-- PostgreSQL ilk kurulum scripti (docker-entrypoint-initdb.d ile otomatik çalışır).
-- Şema, EF Core provider'da migration'larla oluşturulur; NHibernate provider
-- seçildiğinde de aynı şema kullanılır (bkz. docs/03-veritabani-tasarimi.md §5).

CREATE SCHEMA IF NOT EXISTS sanalpos;

-- uuid üretimi ve kriptografik fonksiyonlar için (opsiyonel, ileride gerekebilir)
CREATE EXTENSION IF NOT EXISTS pgcrypto;
