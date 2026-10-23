CREATE TABLE IF NOT EXISTS schema_migrations (
    version     VARCHAR(50)  NOT NULL,
    applied_at  DATETIME(6)  NOT NULL,
    PRIMARY KEY (version)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS inventory_transaction (
    transaction_id  CHAR(36)     NOT NULL,
    request_hash    CHAR(64)     NOT NULL,
    movement_type   VARCHAR(20)  NOT NULL,
    status          VARCHAR(20)  NOT NULL DEFAULT 'COMPLETED',
    source          VARCHAR(30)  NULL,
    user_id         VARCHAR(50)  NULL,
    device_id       VARCHAR(50)  NULL,
    reason_code     VARCHAR(30)  NULL,
    created_at      DATETIME(6)  NOT NULL,
    completed_at    DATETIME(6)  NULL,
    PRIMARY KEY (transaction_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS inventory (
    warehouse   INT            NOT NULL,
    zone        INT            NOT NULL,
    bin         INT            NOT NULL,
    sku         BIGINT         NOT NULL,
    quantity    DECIMAL(18,4)  NOT NULL DEFAULT 0,
    updated_at  DATETIME(6)    NOT NULL,
    PRIMARY KEY (warehouse, zone, bin, sku),
    CONSTRAINT chk_inventory_quantity_non_negative CHECK (quantity >= 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS inventory_history (
    history_id         BIGINT         NOT NULL AUTO_INCREMENT,
    transaction_id     CHAR(36)       NOT NULL,
    document_no        VARCHAR(100)   NULL,
    document_line_id   VARCHAR(100)   NULL,
    movement_type      VARCHAR(20)    NOT NULL,
    warehouse          INT            NOT NULL,
    zone               INT            NOT NULL,
    bin                INT            NOT NULL,
    sku                BIGINT         NOT NULL,
    quantity_delta     DECIMAL(18,4)  NOT NULL,
    quantity_before    DECIMAL(18,4)  NOT NULL,
    quantity_after     DECIMAL(18,4)  NOT NULL,
    counted_quantity   DECIMAL(18,4)  NULL,
    reason_code        VARCHAR(30)    NULL,
    created_at         DATETIME(6)    NOT NULL,
    PRIMARY KEY (history_id),
    KEY ix_inventory_history_transaction_id (transaction_id),
    KEY ix_inventory_history_document (document_no, document_line_id),
    KEY ix_inventory_history_loc (warehouse, zone, bin, sku),
    KEY ix_inventory_history_created (created_at),
    CONSTRAINT fk_inventory_history_transaction
        FOREIGN KEY (transaction_id) REFERENCES inventory_transaction (transaction_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
