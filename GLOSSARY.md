# Warehouse.StockEngine

High-performance, transactional inventory mutations and reference data for warehouse locations and products.

## Language

**Master data**:
Durable reference records—warehouses, zones, bins, and products—that describe where stock can live and what SKU is stored. Managed through `IMasterDataRepository<T>`; not the same layer as stock mutations (`IStockEngine`).
_Avoid_: Stamgegevens (in English-facing specs), configuration tables (too generic)

**Product**:
The identifiable item whose quantity is tracked at a location. Represented by surrogate `id` and business `code` in the `product` table; the fourth dimension of a stock location key.
_Avoid_: Sku (legacy in some docs), item, material

**Stock location**:
The four-part numeric address `(warehouse, zone, bin, product)` used by inventory and `IStockEngine` commands. Values are integers; in a fully wired system they align with master-data surrogate ids even before foreign keys are enforced on `inventory`.
_Avoid_: Locatie (in English-facing specs), address string, barcode location

**Stock mutation**:
An atomic change to quantity at a stock location (inbound, outbound, move, count), with history and idempotency guarantees.
_Avoid_: Transaction (ambiguous with `inventory_transaction`), adjustment

**Warehouse**:
A top-level facility in the master-data hierarchy. Identified by surrogate `id` and unique business `code`; the first dimension of a stock location key.
_Avoid_: Site, building (unless you mean a physical building, not the WMS entity)

**Zone**:
A subdivision of a warehouse. Scoped unique `code` per `warehouse_id`; the second dimension of a stock location key.
_Avoid_: Area, aisle (physical layout terms unless defined as zones)

**Bin**:
A storage slot within a zone. Scoped unique `code` per `zone_id`; the third dimension of a stock location key.
_Avoid_: Location (ambiguous with stock location), slot (informal)