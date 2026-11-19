CREATE TABLE IF NOT EXISTS receipt_confirm (
    confirm_id              CHAR(36)       NOT NULL,
    receipt_line_id         CHAR(36)       NOT NULL,
    engine_transaction_id   CHAR(36)       NOT NULL,
    qty                     DECIMAL(18,4)  NOT NULL,
    confirmed_at            DATETIME(6)    NOT NULL,
    PRIMARY KEY (confirm_id),
    KEY ix_receipt_confirm_line (receipt_line_id),
    CONSTRAINT fk_receipt_confirm_line
        FOREIGN KEY (receipt_line_id) REFERENCES receipt_line (receipt_line_id)
        ON UPDATE RESTRICT ON DELETE RESTRICT,
    CONSTRAINT chk_receipt_confirm_qty_positive CHECK (qty > 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;