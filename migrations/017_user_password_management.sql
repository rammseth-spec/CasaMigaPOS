-- Contraseñas temporales asignadas por un administrador y permiso de gestión de usuarios.
-- Los usuarios existentes conservan su contraseña y no se les exige cambio.
ALTER TABLE app_user ADD COLUMN must_change_password INTEGER NOT NULL DEFAULT 0 CHECK(must_change_password IN(0,1));
INSERT INTO permission VALUES('users.manage');
INSERT INTO role_permission VALUES('Administrator','users.manage');
