using FluentValidation;
using Memoria.Commands;
using Memoria.EventSourcing.Dcb;
using Memoria.Examples.Ecommerce.Dcb.Domain;
using Memoria.Results;
using Memoria.Validation;

namespace Memoria.Examples.Ecommerce.Dcb.Commands;

public record CreateProductCommand(string Name, string Sku, decimal Price) : ICommand;

/// <summary>
/// The shape of the command: what can be checked without reading anything.
/// </summary>
/// <remarks>
/// Registered by <c>AddMemoriaFluentValidation</c>, which scans the assembly for
/// <see cref="AbstractValidator{T}"/> subclasses. Whether a SKU is already taken is not here — that
/// takes a fold of the event store, so it belongs to <see cref="Product.Create"/>.
/// </remarks>
public class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Sku).NotEmpty().MaximumLength(50);
        RuleFor(command => command.Price).GreaterThanOrEqualTo(0);
    }
}

/// <summary>
/// Validate, then read, decide and append — the three steps of a DCB decision.
/// </summary>
public class CreateProductCommandHandler(IDcbDomainService dcb, IValidationService validation)
    : ICommandHandler<CreateProductCommand>
{
    public async Task<Result> Handle(CreateProductCommand command, CancellationToken cancellationToken = default)
    {
        // Runs CreateProductCommandValidator through the registered validation provider. The
        // dispatcher can do this itself with Send(command, validateCommand: true); doing it here
        // makes the handler safe to call either way.
        var validationResult = await validation.Validate(command);
        if (validationResult.IsNotSuccess)
        {
            return validationResult.Failure!;
        }

        // 1. The identifier carries the boundary, so the decision and the events it may read cannot
        //    disagree: this product and this SKU, and nothing else in the catalogue.
        var sku = command.Sku.Trim();
        var productId = new ProductId(Guid.CreateVersion7().ToString(), sku);

        // 2. Read where the boundary stands before folding it. Reading the position afterwards
        //    would let an event slip in between and count as seen when it was not.
        var positionResult = await dcb.GetLatestPosition(productId.Boundary, cancellationToken: cancellationToken);
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

        var refusal = product.Create(productId.Id, command.Name.Trim(), sku, command.Price);
        if (refusal is not null)
        {
            return new Failure(ErrorCode.BadRequest, "Cannot create product", refusal);
        }

        // 3. Append on condition that nothing matching the boundary arrived in between — which is
        //    what stops two requests claiming the same SKU at the same moment.
        return await dcb.SaveAggregate(productId, product,
            new AppendCondition(productId.Boundary, positionResult.Value), cancellationToken);
    }
}
