# Warehouse.StockEngine

High-performance, transactional inventory mutations and reference data for warehouse locations and products.

## Language

**Master data**:
Durable reference records—warehouses, zones, bins, and products—that describe where stock can live and which product is tracked. Managed through `IMasterDataRepository<T>`; not the same layer as stock mutations (`IStockEngine`).
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

## Inbound workflow

**Receipt**:
A durable inbound document with expected lines and received progress; closing or confirming a line results in stock mutations (usually inbound into receiving staging).
_Avoid_: Inbound (use stock mutation), ASN (external document type unless defined as receipt source)

**Putaway**:
The directed placement of received stock from staging into storage bins, expressed as stock mutations (typically move), often as operator tasks.
_Avoid_: Relocate (too generic), transfer (ambiguous with move mutation name)

**Receiving staging**:
A designated storage bin (master data) where receipt confirmation increases stock before putaway moves it to storage.
_Avoid_: Dock, goods-in (physical place unless modeled as a bin)

**Stock workflow**:
Document and task orchestration above `IStockEngine`; it does not change balances itself—it issues stock mutations when work is confirmed.
_Avoid_: WMS (whole product), stock engine (the mutation layer only)

## Outbound workflow

**Shipment**:
A durable outbound document with expected lines and shipped progress; ship confirmation results in stock mutations (typically outbound from ship staging).
_Avoid_: Outbound (use stock mutation), delivery note (paper/logistics sense)

**Picking**:
The outbound workflow that fulfills shipment lines by confirming picks from storage, expressed as stock mutations (typically move into ship staging before outbound).
_Avoid_: Pick (verb without workflow sense), order line (upstream commerce document)

**Soft allocation**:
Workflow-layer commitment of quantity to a shipment line without changing inventory balances or reserving stock in the engine.
_Avoid_: Reservation (when you mean engine-enforced hold), allocate (ambiguous with bin assignment)

**Ship staging**:
A designated storage bin (master data) where picked goods accumulate before ship confirmation outbound.
_Avoid_: Dock, loading bay (physical place unless modeled as a bin)
