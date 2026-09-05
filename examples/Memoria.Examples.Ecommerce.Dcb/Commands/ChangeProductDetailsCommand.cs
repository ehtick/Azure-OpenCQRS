using FluentValidation;
using Memoria.Commands;
using Memoria.EventSourcing;
using Memoria.EventSourcing.Dcb;
using Memoria.Examples.Ecommerce.Dcb.Domain;
using Memoria.Examples.Ecommerce.Dcb.Notifications;
using Memoria.Results;
using Memoria.Validation;

namespace Memoria.Examples.Ecommerce.Dcb.Commands;

public record ChangeProductDetailsCommand(string ProductId, string Name, decimal Price) : ICommand<CommandResponse>;

public class ChangeProductDetailsCommandValidator : AbstractValidator<ChangeProductDetailsCommand>
{
    public ChangeProductDetailsCommandValidator()
    {
        RuleFor(command => command.ProductId).NotEmpty();
        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Price).GreaterThanOrEqualTo(0);
    }
}

/// <summary>
/// Read, decide, append — through the store's own snapshot, rather than folding from scratch.
/// </summary>
/// <remarks>
/// <see cref="ReadMode.SnapshotWithNewEventsOrCreate"/> is the fullest of the four: it takes the
/// stored snapshot, applies whatever has been appended since, and hands back an empty model rather
/// than null when there is nothing at all. <see cref="Product.ChangeDetails"/> then refuses that
/// empty case on its own terms. <c>SaveAggregate</c> writes the snapshot forward again, so the next
/// read starts from here instead of replaying the product's whole history.
/// </remarks>
public class ChangeProductDetailsCommandHandler(
    IDcbDomainService dcb,
    IValidationService validation,
    TimeProvider timeProvider)
    : ICommandHandler<ChangeProductDetailsCommand, CommandResponse>
{
    public async Task<Result<CommandResponse>> Handle(ChangeProductDetailsCommand command,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await validation.Validate(command);
        if (validationResult.IsNotSuccess)
        {
            return validationResult.Failure!;
        }

        // Renaming or repricing depends only on this product, so it reads only this product's tag.
        var productId = new ProductId(command.ProductId);

        var positionResult = await dcb.GetLatestPosition(productId.Boundary,
            cancellationToken: cancellationToken);
        if (positionResult.IsNotSuccess)
        {
            return positionResult.Failure!;
        }

        var productResult = await dcb.GetAggregate(productId, ReadMode.SnapshotWithNewEventsOrCreate,
            cancellationToken);
        if (productResult.IsNotSuccess)
        {
            return productResult.Failure!;
        }

        var product = productResult.Value!;
        var name = command.Name.Trim();

        var refusal = product.ChangeDetails(name, command.Price);
        if (refusal is not null)
        {
            return new Failure(ErrorCode.BadRequest, "Cannot change product", refusal);
        }

        var saveResult = await dcb.SaveAggregate(productId, product,
            new AppendCondition(productId.Boundary, positionResult.Value), cancellationToken);

        if (saveResult.IsNotSuccess)
        {
            return saveResult.Failure!;
        }

        return new CommandResponse
        {
            Notifications =
            [
                new ProductDetailsChangedNotification(command.ProductId, name, command.Price,
                    timeProvider.GetUtcNow())
            ]
        };
    }
}
