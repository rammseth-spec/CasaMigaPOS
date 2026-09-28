CREATE TABLE product_photo_asset (
 id TEXT PRIMARY KEY NOT NULL CHECK(length(id)=36),
 product_id TEXT NOT NULL REFERENCES product(id) ON DELETE RESTRICT,
 layout_version INTEGER NOT NULL CHECK(layout_version=1),
 provenance TEXT NOT NULL CHECK(provenance IN ('PROVISIONAL','REAL')),
 original_name TEXT NOT NULL CHECK(length(original_name) BETWEEN 1 AND 255 AND instr(original_name,'/')=0 AND instr(original_name,char(92))=0 AND instr(original_name,':')=0),
 original_format TEXT NOT NULL CHECK(original_format IN ('jpeg','png')),
 original_sha256 TEXT NOT NULL CHECK(length(original_sha256)=64 AND original_sha256 NOT GLOB '*[^0-9A-F]*'),
 original_bytes INTEGER NOT NULL CHECK(original_bytes BETWEEN 1 AND 26214400),
 original_width INTEGER NOT NULL CHECK(original_width BETWEEN 1 AND 12000),
 original_height INTEGER NOT NULL CHECK(original_height BETWEEN 1 AND 12000),
 orientation INTEGER NOT NULL CHECK(orientation BETWEEN 1 AND 8),
 crop_x INTEGER NOT NULL CHECK(crop_x>=0),
 crop_y INTEGER NOT NULL CHECK(crop_y>=0),
 crop_edge INTEGER NOT NULL CHECK(crop_edge>=256),
 pipeline_version INTEGER NOT NULL CHECK(pipeline_version=1),
 thumbnail_sha256 TEXT NOT NULL CHECK(length(thumbnail_sha256)=64 AND thumbnail_sha256 NOT GLOB '*[^0-9A-F]*'),
 thumbnail_bytes INTEGER NOT NULL CHECK(thumbnail_bytes BETWEEN 1 AND 2097152),
 thumbnail_edge INTEGER NOT NULL CHECK(thumbnail_edge BETWEEN 256 AND 512),
 jpeg_quality INTEGER NOT NULL CHECK(jpeg_quality=85),
 created_utc TEXT NOT NULL,
 created_by TEXT NOT NULL REFERENCES app_user(id) ON DELETE RESTRICT,
 terminal_id TEXT NOT NULL REFERENCES terminal(id) ON DELETE RESTRICT,
 UNIQUE(product_id,id),
 CHECK(original_width*original_height<=48000000),
 CHECK(crop_x+crop_edge<=CASE WHEN orientation IN (5,6,7,8) THEN original_height ELSE original_width END),
 CHECK(crop_y+crop_edge<=CASE WHEN orientation IN (5,6,7,8) THEN original_width ELSE original_height END),
 CHECK(thumbnail_edge=CASE WHEN crop_edge<512 THEN crop_edge ELSE 512 END)
) STRICT;
CREATE TABLE product_photo (
 product_id TEXT PRIMARY KEY NOT NULL REFERENCES product(id) ON DELETE RESTRICT,
 current_photo_id TEXT,
 revision INTEGER NOT NULL CHECK(revision>=1),
 last_operation_id TEXT NOT NULL UNIQUE CHECK(length(last_operation_id)=36),
 modified_utc TEXT NOT NULL,
 modified_by TEXT NOT NULL REFERENCES app_user(id) ON DELETE RESTRICT,
 terminal_id TEXT NOT NULL REFERENCES terminal(id) ON DELETE RESTRICT,
 FOREIGN KEY(product_id,current_photo_id) REFERENCES product_photo_asset(product_id,id) ON DELETE RESTRICT
) STRICT;
-- Metadata is historical; this does not require retention of retired physical files.
CREATE TRIGGER product_photo_asset_no_delete BEFORE DELETE ON product_photo_asset
BEGIN SELECT RAISE(ABORT,'Photo metadata is historical'); END;
CREATE TRIGGER product_photo_asset_immutable BEFORE UPDATE ON product_photo_asset
WHEN NEW.id<>OLD.id OR NEW.product_id<>OLD.product_id OR NEW.layout_version<>OLD.layout_version
 OR NEW.provenance<>OLD.provenance OR NEW.original_name<>OLD.original_name
 OR NEW.original_format<>OLD.original_format OR NEW.original_sha256<>OLD.original_sha256
 OR NEW.original_bytes<>OLD.original_bytes OR NEW.original_width<>OLD.original_width OR NEW.original_height<>OLD.original_height
 OR NEW.orientation<>OLD.orientation OR NEW.crop_x<>OLD.crop_x OR NEW.crop_y<>OLD.crop_y OR NEW.crop_edge<>OLD.crop_edge
 OR NEW.pipeline_version<>OLD.pipeline_version OR NEW.thumbnail_edge<>OLD.thumbnail_edge OR NEW.jpeg_quality<>OLD.jpeg_quality
 OR NEW.created_utc<>OLD.created_utc OR NEW.created_by<>OLD.created_by OR NEW.terminal_id<>OLD.terminal_id
BEGIN SELECT RAISE(ABORT,'Original photo metadata is immutable'); END;
CREATE TRIGGER product_photo_no_delete BEFORE DELETE ON product_photo
BEGIN SELECT RAISE(ABORT,'Photo revision must be preserved'); END;
CREATE TRIGGER product_photo_revision BEFORE UPDATE ON product_photo
WHEN NEW.product_id<>OLD.product_id OR NEW.revision<>OLD.revision+1
BEGIN SELECT RAISE(ABORT,'Invalid photo revision'); END;
