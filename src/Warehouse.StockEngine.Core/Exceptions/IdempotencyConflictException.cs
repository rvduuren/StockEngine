namespace Warehouse.StockEngine.Core.Exceptions;

public sealed class IdempotencyConflictException : Exception
{
    public IdempotencyConflictException(Guid transactionId)
        : base($"TransactionId '{transactionId}' was already used with a different request payload.")
    {
        TransactionId = transactionId;
    }

    public Guid TransactionId { get; }
}
