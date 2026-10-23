namespace Warehouse.StockEngine.Infrastructure.MariaDb.Sql;

internal static class StockQueries
{
    public const string ClaimTransaction = """
        INSERT INTO inventory_transaction (
            transaction_id, request_hash, movement_type, status,
            source, user_id, device_id, reason_code,
            created_at, completed_at)
        VALUES (
            @TransactionId, @RequestHash, @MovementType, 'COMPLETED',
            @Source, @UserId, @DeviceId, @ReasonCode,
            UTC_TIMESTAMP(6), NULL);
        """;

    public const string CompleteTransaction = """
        UPDATE inventory_transaction
        SET completed_at = UTC_TIMESTAMP(6),
            status = 'COMPLETED'
        WHERE transaction_id = @TransactionId;
        """;

    public const string GetTransactionHash = """
        SELECT request_hash
        FROM inventory_transaction
        WHERE transaction_id = @TransactionId;
        """;

    public const string CountHistoryRows = """
        SELECT COUNT(*)
        FROM inventory_history
        WHERE transaction_id = @TransactionId;
        """;

    public const string InsertHistory = """
        INSERT INTO inventory_history (
            transaction_id, document_no, document_line_id, movement_type,
            warehouse, zone, bin, sku,
            quantity_delta, quantity_before, quantity_after,
            counted_quantity, reason_code, created_at)
        VALUES (
            @TransactionId, @DocumentNo, @DocumentLineId, @MovementType,
            @Warehouse, @Zone, @Bin, @Sku,
            @QuantityDelta, @QuantityBefore, @QuantityAfter,
            @CountedQuantity, @ReasonCode, UTC_TIMESTAMP(6));
        """;

    public const string GetQuantity = """
        SELECT quantity
        FROM inventory
        WHERE warehouse = @Warehouse
          AND zone = @Zone
          AND bin = @Bin
          AND sku = @Sku;
        """;

    public const string LockQuantity = """
        SELECT quantity
        FROM inventory
        WHERE warehouse = @Warehouse
          AND zone = @Zone
          AND bin = @Bin
          AND sku = @Sku
        FOR UPDATE;
        """;

    public const string EnsureInventoryRow = """
        INSERT INTO inventory (warehouse, zone, bin, sku, quantity, updated_at)
        VALUES (@Warehouse, @Zone, @Bin, @Sku, 0, UTC_TIMESTAMP(6))
        ON DUPLICATE KEY UPDATE warehouse = warehouse;
        """;

    public const string IncreaseQuantity = """
        UPDATE inventory
        SET quantity = quantity + @Quantity,
            updated_at = UTC_TIMESTAMP(6)
        WHERE warehouse = @Warehouse
          AND zone = @Zone
          AND bin = @Bin
          AND sku = @Sku;
        """;

    public const string DecreaseQuantityIfSufficient = """
        UPDATE inventory
        SET quantity = quantity - @Quantity,
            updated_at = UTC_TIMESTAMP(6)
        WHERE warehouse = @Warehouse
          AND zone = @Zone
          AND bin = @Bin
          AND sku = @Sku
          AND quantity >= @Quantity;
        """;

    public const string SetQuantity = """
        UPDATE inventory
        SET quantity = @Quantity,
            updated_at = UTC_TIMESTAMP(6)
        WHERE warehouse = @Warehouse
          AND zone = @Zone
          AND bin = @Bin
          AND sku = @Sku;
        """;
}
