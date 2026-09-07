CREATE TABLE terminal (
    id TEXT PRIMARY KEY NOT NULL,
    name TEXT NOT NULL CHECK(length(trim(name)) BETWEEN 1 AND 100),
    created_utc TEXT NOT NULL
) STRICT;
CREATE TABLE category (
    id TEXT PRIMARY KEY NOT NULL,
    name TEXT NOT NULL CHECK(length(trim(name)) BETWEEN 1 AND 100),
    active INTEGER NOT NULL DEFAULT 1 CHECK(active IN (0,1))
) STRICT;
CREATE TABLE product (
    id TEXT PRIMARY KEY NOT NULL,
    sku TEXT NULL CHECK(sku IS NULL OR length(trim(sku)) > 0),
    name TEXT NOT NULL CHECK(length(trim(name)) BETWEEN 1 AND 160),
    category_id TEXT NOT NULL REFERENCES category(id) ON DELETE RESTRICT,
    description TEXT NOT NULL DEFAULT '',
    price_cents INTEGER NOT NULL CHECK(price_cents >= 0),
    track_inventory INTEGER NOT NULL CHECK(track_inventory IN (0,1)),
    active INTEGER NOT NULL DEFAULT 1 CHECK(active IN (0,1)),
    created_utc TEXT NOT NULL,
    modified_utc TEXT NOT NULL
) STRICT;
CREATE UNIQUE INDEX ux_product_sku ON product(sku) WHERE sku IS NOT NULL;
CREATE INDEX ix_product_category ON product(category_id);
