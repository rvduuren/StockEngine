using System.Data.Common;
using Dapper;
using MySqlConnector;
using Warehouse.StockEngine.Core.Interfaces;
using Warehouse.StockEngine.Core.MasterData.Interfaces;
using Warehouse.StockEngine.Core.StockWorkflow.Receipt.Enums;
using Warehouse.StockEngine.Core.StockWorkflow.Receipt.Exceptions;
using Warehouse.StockEngine.Core.StockWorkflow.Receipt.Interfaces;
using Warehouse.StockEngine.Core.StockWorkflow.Receipt.Models;

namespace Warehouse.StockEngine.Infrastructure.MariaDb.StockWorkflow.Receipt;

public sealed class MariaDbReceiptWorkflow(
    IDbConnectionFactory connectionFactory,
    IMasterDataRepository<Core.MasterData.Entities.Warehouse> warehouses,
    IMasterDataRepository<Core.MasterData.Entities.Product> products,
    IZoneRepository zones,
    IBinRepository bins) : IReceiptWorkflow
{
    private const string StatusOpen = "OPEN";
    private const string StatusClosed = "CLOSED";

    public async Task<ReceivingStagingConfig> SetReceivingStagingAsync(
        SetReceivingStagingRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await ValidateStagingLocationAsync(request.WarehouseId, request.ZoneId, request.BinId, cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        var now = DateTime.UtcNow;

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO receiving_staging_config (warehouse_id, zone_id, bin_id, updated_at)
            VALUES (@WarehouseId, @ZoneId, @BinId, @UpdatedAt)
            ON DUPLICATE KEY UPDATE
                zone_id = VALUES(zone_id),
                bin_id = VALUES(bin_id),
                updated_at = VALUES(updated_at);
            """,
            new
            {
                request.WarehouseId,
                request.ZoneId,
                request.BinId,
                UpdatedAt = now
            },
            cancellationToken: cancellationToken));

        return new ReceivingStagingConfig
        {
            WarehouseId = request.WarehouseId,
            ZoneId = request.ZoneId,
            BinId = request.BinId
        };
    }

    public async Task<ReceivingStagingConfig?> GetReceivingStagingAsync(
        int warehouseId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        return await connection.QueryFirstOrDefaultAsync<ReceivingStagingConfig>(new CommandDefinition(
            """
            SELECT warehouse_id AS WarehouseId, zone_id AS ZoneId, bin_id AS BinId
            FROM receiving_staging_config
            WHERE warehouse_id = @WarehouseId;
            """,
            new { WarehouseId = warehouseId },
            cancellationToken: cancellationToken));
    }

    public async Task<ReceiptDetail> CreateReceiptAsync(
        CreateReceiptRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateCreateRequestShape(request);

        if (await warehouses.GetByIdAsync(request.WarehouseId, cancellationToken) is null)
        {
            throw new ReceiptWorkflowValidationException(
                $"Warehouse {request.WarehouseId} was not found.");
        }

        if (await GetReceivingStagingAsync(request.WarehouseId, cancellationToken) is null)
        {
            throw new ReceivingStagingNotConfiguredException(request.WarehouseId);
        }

        foreach (var line in request.Lines)
        {
            if (await products.GetByIdAsync(line.ProductId, cancellationToken) is null)
            {
                throw new ReceiptWorkflowValidationException(
                    $"Product {line.ProductId} was not found.");
            }
        }

        if (await GetReceiptByDocumentNoAsync(request.WarehouseId, request.DocumentNo, cancellationToken) is not null)
        {
            throw new DuplicateReceiptDocumentException(request.WarehouseId, request.DocumentNo);
        }

        var receiptId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            await InsertReceiptAsync(connection, transaction, receiptId, request, createdAt, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (MySqlException ex) when (ex.ErrorCode is MySqlErrorCode.DuplicateKeyEntry)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new DuplicateReceiptDocumentException(request.WarehouseId, request.DocumentNo);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return (await GetReceiptByIdAsync(receiptId, cancellationToken))!;
    }

    private static async Task InsertReceiptAsync(
        DbConnection connection,
        DbTransaction transaction,
        Guid receiptId,
        CreateReceiptRequest request,
        DateTime createdAt,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO receipt (receipt_id, warehouse_id, document_no, status, created_at)
                VALUES (@ReceiptId, @WarehouseId, @DocumentNo, @Status, @CreatedAt);
                """,
                new
                {
                    ReceiptId = receiptId.ToString(),
                    request.WarehouseId,
                    request.DocumentNo,
                    Status = StatusOpen,
                    CreatedAt = createdAt
                },
            transaction,
            cancellationToken: cancellationToken));

        foreach (var line in request.Lines)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO receipt_line
                    (receipt_line_id, receipt_id, line_id, product_id, expected_qty, received_qty)
                VALUES (@ReceiptLineId, @ReceiptId, @LineId, @ProductId, @ExpectedQty, 0);
                """,
                new
                {
                    ReceiptLineId = Guid.NewGuid().ToString(),
                    ReceiptId = receiptId.ToString(),
                    line.LineId,
                    line.ProductId,
                    ExpectedQty = line.ExpectedQuantity
                },
                transaction,
                cancellationToken: cancellationToken));
        }
    }

    public async Task<ReceiptDetail?> GetReceiptByIdAsync(
        Guid receiptId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        var header = await connection.QueryFirstOrDefaultAsync<ReceiptHeaderRow>(new CommandDefinition(
            """
            SELECT receipt_id AS ReceiptId,
                   warehouse_id AS WarehouseId,
                   document_no AS DocumentNo,
                   status AS Status,
                   created_at AS CreatedAt
            FROM receipt
            WHERE receipt_id = @ReceiptId;
            """,
            new { ReceiptId = receiptId.ToString() },
            cancellationToken: cancellationToken));

        if (header is null)
        {
            return null;
        }

        var lines = await LoadLinesAsync(connection, header.ReceiptId.ToString(), cancellationToken);
        return MapDetail(header, lines);
    }

    public async Task<ReceiptDetail?> GetReceiptByDocumentNoAsync(
        int warehouseId,
        string documentNo,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentNo);

        await using var connection = await OpenConnectionAsync(cancellationToken);

        var header = await connection.QueryFirstOrDefaultAsync<ReceiptHeaderRow>(new CommandDefinition(
            """
            SELECT receipt_id AS ReceiptId,
                   warehouse_id AS WarehouseId,
                   document_no AS DocumentNo,
                   status AS Status,
                   created_at AS CreatedAt
            FROM receipt
            WHERE warehouse_id = @WarehouseId AND document_no = @DocumentNo;
            """,
            new { WarehouseId = warehouseId, DocumentNo = documentNo },
            cancellationToken: cancellationToken));

        if (header is null)
        {
            return null;
        }

        var lines = await LoadLinesAsync(connection, header.ReceiptId.ToString(), cancellationToken);
        return MapDetail(header, lines);
    }

    public async Task<IReadOnlyList<ReceiptSummary>> ListOpenReceiptsAsync(
        int warehouseId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<ReceiptHeaderRow>(new CommandDefinition(
            """
            SELECT receipt_id AS ReceiptId,
                   warehouse_id AS WarehouseId,
                   document_no AS DocumentNo,
                   status AS Status,
                   created_at AS CreatedAt
            FROM receipt
            WHERE warehouse_id = @WarehouseId AND status = @Status
            ORDER BY created_at, document_no;
            """,
            new { WarehouseId = warehouseId, Status = StatusOpen },
            cancellationToken: cancellationToken));

        return rows.Select(MapSummary).ToList();
    }

    private async Task ValidateStagingLocationAsync(
        int warehouseId,
        int zoneId,
        int binId,
        CancellationToken cancellationToken)
    {
        if (await warehouses.GetByIdAsync(warehouseId, cancellationToken) is null)
        {
            throw new ReceiptWorkflowValidationException(
                $"Warehouse {warehouseId} was not found.");
        }

        var zone = await zones.GetByIdAsync(zoneId, cancellationToken);
        if (zone is null)
        {
            throw new ReceiptWorkflowValidationException($"Zone {zoneId} was not found.");
        }

        if (zone.WarehouseId != warehouseId)
        {
            throw new ReceiptWorkflowValidationException(
                $"Zone {zoneId} does not belong to warehouse {warehouseId}.");
        }

        var bin = await bins.GetByIdAsync(binId, cancellationToken);
        if (bin is null)
        {
            throw new ReceiptWorkflowValidationException($"Bin {binId} was not found.");
        }

        if (bin.ZoneId != zoneId)
        {
            throw new ReceiptWorkflowValidationException(
                $"Bin {binId} does not belong to zone {zoneId}.");
        }
    }

    private static void ValidateCreateRequestShape(CreateReceiptRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DocumentNo))
        {
            throw new ReceiptWorkflowValidationException("Document number is required.");
        }

        if (request.Lines is null || request.Lines.Count == 0)
        {
            throw new ReceiptWorkflowValidationException("At least one receipt line is required.");
        }

        var lineIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in request.Lines)
        {
            if (string.IsNullOrWhiteSpace(line.LineId))
            {
                throw new ReceiptWorkflowValidationException("Each receipt line requires a line id.");
            }

            if (!lineIds.Add(line.LineId))
            {
                throw new ReceiptWorkflowValidationException(
                    $"Duplicate line id '{line.LineId}' in create request.");
            }

            if (line.ExpectedQuantity <= 0)
            {
                throw new ReceiptWorkflowValidationException(
                    $"Expected quantity must be positive for line '{line.LineId}'.");
            }
        }
    }

    private static async Task<IReadOnlyList<ReceiptLineRow>> LoadLinesAsync(
        DbConnection connection,
        string receiptId,
        CancellationToken cancellationToken)
    {
        var rows = await connection.QueryAsync<ReceiptLineRow>(new CommandDefinition(
            """
            SELECT line_id AS LineId,
                   product_id AS ProductId,
                   expected_qty AS ExpectedQuantity,
                   received_qty AS ReceivedQuantity
            FROM receipt_line
            WHERE receipt_id = @ReceiptId
            ORDER BY line_id;
            """,
            new { ReceiptId = receiptId },
            cancellationToken: cancellationToken));

        return rows.ToList();
    }

    private static ReceiptDetail MapDetail(ReceiptHeaderRow header, IReadOnlyList<ReceiptLineRow> lines) =>
        new()
        {
            ReceiptId = header.ReceiptId,
            WarehouseId = header.WarehouseId,
            DocumentNo = header.DocumentNo,
            Status = ParseStatus(header.Status),
            CreatedAt = header.CreatedAt,
            Lines = lines.Select(MapLine).ToList()
        };

    private static ReceiptSummary MapSummary(ReceiptHeaderRow header) =>
        new()
        {
            ReceiptId = header.ReceiptId,
            WarehouseId = header.WarehouseId,
            DocumentNo = header.DocumentNo,
            Status = ParseStatus(header.Status),
            CreatedAt = header.CreatedAt
        };

    private static ReceiptLineDetail MapLine(ReceiptLineRow line) =>
        new()
        {
            LineId = line.LineId,
            ProductId = line.ProductId,
            ExpectedQuantity = line.ExpectedQuantity,
            ReceivedQuantity = line.ReceivedQuantity,
            RemainingQuantity = line.ExpectedQuantity - line.ReceivedQuantity
        };

    private static ReceiptStatus ParseStatus(string status) =>
        status switch
        {
            StatusOpen => ReceiptStatus.Open,
            StatusClosed => ReceiptStatus.Closed,
            _ => throw new InvalidOperationException($"Unknown receipt status '{status}'.")
        };

    private async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = connectionFactory.CreateConnection();
        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private sealed class ReceiptHeaderRow
    {
        public Guid ReceiptId { get; init; }
        public int WarehouseId { get; init; }
        public string DocumentNo { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public DateTime CreatedAt { get; init; }
    }

    private sealed class ReceiptLineRow
    {
        public string LineId { get; init; } = string.Empty;
        public int ProductId { get; init; }
        public decimal ExpectedQuantity { get; init; }
        public decimal ReceivedQuantity { get; init; }
    }
}