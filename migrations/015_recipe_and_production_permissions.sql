-- Versioned authorization only; no production services or role bypasses.
INSERT INTO permission VALUES
('recipe.view'),('recipe.configure'),('production.view'),('production.create'),('production.confirm');
INSERT INTO role_permission VALUES
('Administrator','recipe.view'),
('Administrator','recipe.configure'),
('Administrator','production.view'),
('Administrator','production.create'),
('Administrator','production.confirm'),
('Manager','recipe.view'),
('Manager','production.view'),
('Manager','production.create'),
('Manager','production.confirm');
