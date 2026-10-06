# Security policy

Casa Miga POS is a private commercial-development repository.

Never commit or attach production SQLite databases or sidecars, `.cmbackup` files, passwords, API keys, tokens, review credentials, `terminal.json`, local development settings, production-identifying customer/sales/employee data, or raw production logs/events.

Use synthetic or explicitly sanitized test data.

Critical commercial operations must remain atomic. Printing and other physical effects occur after the authoritative transaction commit. Completed sales and inventory movements are not physically deleted; corrections remain traceable. Applied migrations are immutable.
