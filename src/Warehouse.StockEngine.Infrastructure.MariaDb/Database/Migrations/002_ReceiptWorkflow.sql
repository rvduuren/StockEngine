CREATE TABLE IF NOT EXISTS receiving_staging_config (
    warehouse_id  INT            NOT NULL,
    zone_id       INT            NOT NULL,
    bin_id        INT            NOT NULL,
    updated_at    DATETIME(6)    NOT NULL,
    PRIMARY KEY (warehouse_id),
    KEY ix_receiving_staging_zone (zone_id),
    KEY ix_receiving_staging_bin (bin_id),
    CONSTRAINT fk_receiving_staging_warehouse
        FOREIGN KEY (warehouse_id) REFERENCES warehouse (id)
        ON UPDATE RESTRICT ON DELETE RESTRICT,
    CONSTRAINT fk_receiving_staging_zone
        FOREIGN KEY (zone_id) REFERENCES zone (id)
        ON UPDATE RESTRICT ON DELETE RESTRICT,
    CONSTRAINT fk_receiving_staging_bin
        FOREIGN KEY (bin_id) REFERENCES bin (id)
        ON UPDATE RESTRICT ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS receipt (
    receipt_id    CHAR(36)       NOT NULL,
    warehouse_id  INT            NOT NULL,
    document_no   VARCHAR(100)   NOT NULL,
    status        VARCHAR(20)    NOT NULL,
    created_at    DATETIME(6)    NOT NULL,
    PRIMARY KEY (receipt_id),
    UNIQUE KEY uq_receipt_warehouse_document (warehouse_id, document_no),
    KEY ix_receipt_warehouse_status (warehouse_id, status),
    CONSTRAINT fk_receipt_warehouse
        FOREIGN KEY (warehouse_id) REFERENCES warehouse (id)
        ON UPDATE RESTRICT ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS receipt_line (
    receipt_line_id  CHAR(36)       NOT NULL,
    receipt_id       CHAR(36)       NOT NULL,
    line_id          VARCHAR(100)   NOT NULL,
    product_id       INT            NOT NULL,
    expected_qty     DECIMAL(18,4)  NOT NULL,
    received_qty     DECIMAL(18,4)  NOT NULL DEFAULT 0,
    PRIMARY KEY (receipt_line_id),
    UNIQUE KEY uq_receipt_line_business (receipt_id, line_id),
    KEY ix_receipt_line_receipt (receipt_id),
    KEY ix_receipt_line_product (product_id),
    CONSTRAINT fk_receipt_line_receipt
        FOREIGN KEY (receipt_id) REFERENCES receipt (receipt_id)
        ON UPDATE RESTRICT ON DELETE RESTRICT,
    CONSTRAINT fk_receipt_line_product
        FOREIGN KEY (product_id) REFERENCES product (id)
        ON UPDATE RESTRICT ON DELETE RESTRICT,
    CONSTRAINT chk_receipt_line_expected_positive CHECK (expected_qty > 0),
    CONSTRAINT chk_receipt_line_received_non_negative CHECK (received_qty >= 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;