# ADR-003 — Dinero, cantidades y costos

Estado: centavos implementados; política de costeo propuesta para su fase.

MXN: Int64 de centavos y operaciones `checked`. Nunca double/float. `Money.FromPesos(decimal)` rechaza más de dos decimales en vez de redondear silenciosamente. Money permite valores negativos para diferencias y reversos; precios y pagos se validarán según su contexto, con restricciones en DB. El precio de catálogo ya exige no negativo.

Costos por gramo NO se guardarán como centavos enteros por gramo: perderían precisión. Propuesta: decimal base 10 con escala explícita o entero escalado de alta precisión, conservando costo total y cantidad original. Redondear importes finales a centavos con política documentada y reparto determinista de residuos. No decidir impuestos ni redondeo fiscal sin requisitos de negocio.

Ejemplo exacto: 429 MXN / 25000 g = 0.01716 MXN/g; 1000 g = 17.16 MXN. Guardar snapshots de costos al producir/vender; no recalcular históricos con precio actual.

Unidades futuras: g, ml, pieza. Kg→g y l→ml multiplican por 1000; nunca convertir peso a volumen sin dato explícito. Cantidades fraccionarias se diseñarán con escala y pruebas antes de recetas. No hay conversiones implementadas todavía.
