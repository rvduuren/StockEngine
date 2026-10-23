namespace Warehouse.StockEngine.Infrastructure.MariaDb.Sql;

internal static class StockQueries
{
    public const string ClaimTransaction = """
        INSERT INTO inventory_transaction (transaction_id, request_hash, movement_type, created_at)
        VALUES (@TransactionId, @RequestHash, @MovementType, UTC_TIMESTAMP(6));
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
            warehouse_code, location_code, item_code,
            quantity_delta, quantity_before, quantity_after, created_at)
        VALUES (
            @TransactionId, @DocumentNo, @DocumentLineId, @MovementType,
            @WarehouseCode, @LocationCode, @ItemCode,
            @QuantityDelta, @QuantityBefore, @QuantityAfter, UTC_TIMESTAMP(6));
        """;

    public const string GetQuantity = """
        SELECT quantity
        FROM inventory
        WHERE warehouse_code = @WarehouseCode
          AND location_code = @LocationCode
          AND item_code = @ItemCode;
        """;

    public const string LockQuantity = """
        SELECT quantity
        FROM inventory
        WHERE warehouse_code = @WarehouseCode
          AND location_code = @LocationCode
          AND item_code = @ItemCode
        FOR UPDATE;
        """;

    public const string EnsureInventoryRow = """
        INSERT INTO inventory (warehouse_code, location_code, item_code, quantity, updated_at)
        VALUES (@WarehouseCode, @LocationCode, @ItemCode, 0, UTC_TIMESTAMP(6))
        ON DUPLICATE KEY UPDATE warehouse_code = warehouse_code;
        """;

    public const string IncreaseQuantity = """
        UPDATE inventory
        SET quantity = quantity + @Quantity,
            updated_at = UTC_TIMESTAMP(6)
        WHERE warehouse_code = @WarehouseCode
          AND location_code = @LocationCode
          AND item_code = @ItemCode;
        """;

    public const string DecreaseQuantityIfSufficient = """
        UPDATE inventory
        SET quantity = quantity - @Quantity,
            updated_at = UTC_TIMESTAMP(6)
        WHERE warehouse_code = @WarehouseCode
          AND location_code = @LocationCode
          AND item_code = @ItemCode
          AND quantity >= @Quantity;
        """;

    public const string SetQuantity = """
        UPDATE inventory
        SET quantity = @Quantity,
            updated_at = UTC_TIMESTAMP(6)
        WHERE warehouse_code = @WarehouseCode
          AND location_code = @LocationCode
          AND item_code = @ItemCode;
        """;
}
