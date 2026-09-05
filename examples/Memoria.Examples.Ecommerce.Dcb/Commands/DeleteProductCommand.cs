using FluentValidation;
using Memoria.Commands;
using Memoria.EventSourcing.Dcb;
using Memoria.Examples.Ecommerce.Dcb.Domain;
using Memoria.Examples.Ecommerce.Dcb.Notifications;
using Memoria.Results;
using Memoria.Validation;

namespace Memoria.Examples.Ecommerce.Dcb.Commands;

public record DeleteProductCommand(string ProductId) : ICommand<CommandResponse>;

public class DeleteProductCommandValidator : AbstractValidator<DeleteProductCommand>
{
    public DeleteProductCommandValidator()
    {
        RuleFor(command => command.ProductId).NotEmpty();
    }
}

/// <summary>
/// Read, decide, append — over a narrower boundary than creating one needs.
/// </summary>
/// <remarks>
/// <see cref="ProductId"/> is the identifier for the product itself, so the store can build the
/// model and fold it; creating a product uses <see cref="ProductCreationId"/> instead, whose wider
/// boundary also takes in the SKU. One identifier could not honestly carry both.
/// </remarks>
public class DeleteProductCommandHandler(IDcbDomainService dcb, IValidationService validation)
    : ICommandHandler<DeleteProductCommand, CommandResponse>
{
    public async Task<Result<CommandResponse>> Handle(DeleteProductCommand command,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await validation.Validate(command);
        if (validationResult.IsNotSuccess)
        {
            return validationResult.Failure!;
        }

        // Deleting depends only on this product's own history, so it reads only this product's tag.
        // The SKU is never taken from the caller — it comes out of the fold, so a forged one cannot
        // be used to free somebody else's code.
        var productId = new ProductId(command.ProductId);

        var positionResult = await dcb.GetLatestPosition(productId.Boundary,
            cancellationToken: cancellationToken);
        if (positionResult.IsNotSuccess)
        {
            return positionResult.Failure!;
        }

        var productResult = await dcb.GetInMemoryAggregate(productId, cancellationToken);
        if (productResult.IsNotSuccess)
        {
            return productResult.Failure!;
        }

        var product = productResult.Value!;

        var refusal = product.Delete();
        if (refusal is not null)
        {
            return new Failure(ErrorCode.NotFound, "Cannot delete product", refusal);
        }

        // Conditioned on the boundary that was folded. The event is written under the SKU tag too,
        // which is enough to make a concurrent creation claiming that SKU fail.
        var saveResult = await dcb.SaveEvents([..product.UncommittedEvents],
            new AppendCondition(productId.Boundary, positionResult.Value), cancellationToken);

        if (saveResult.IsNotSuccess)
        {
            return saveResult.Failure!;
        }

        return new CommandResponse
        {
            Notifications = [new ProductDeletedNotification(command.ProductId)]
        };
    }
}
