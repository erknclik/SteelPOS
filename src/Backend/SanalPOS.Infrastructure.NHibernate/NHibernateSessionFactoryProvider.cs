using NHibernate;
using NHibernate.Cfg;
using NHibernate.Cfg.MappingSchema;
using NHibernate.Dialect;
using NHibernate.Driver;
using NHibernate.Mapping.ByCode;
using SanalPOS.Infrastructure.NHibernate.Mappings;

namespace SanalPOS.Infrastructure.NHibernate;

/// <summary>NHibernate SessionFactory kurulumunu ve mapping-by-code konfigürasyonunu yapar.</summary>
public static class NHibernateSessionFactoryProvider
{
    public static ISessionFactory Build(string connectionString)
    {
        var configuration = new Configuration();
        configuration.DataBaseIntegration(db =>
        {
            db.ConnectionString = connectionString;
            db.Dialect<PostgreSQL83Dialect>();
            db.Driver<NpgsqlDriver>();
            db.KeywordsAutoImport = Hbm2DDLKeyWords.AutoQuote;
            db.LogFormattedSql = false;
        });

        configuration.AddMapping(BuildMappings());
        return configuration.BuildSessionFactory();
    }

    private static HbmMapping BuildMappings()
    {
        var mapper = new ModelMapper();
        mapper.AddMappings(typeof(MerchantMap).Assembly.GetExportedTypes()
            .Where(t => t.Namespace == typeof(MerchantMap).Namespace));
        return mapper.CompileMappingForAllExplicitlyAddedEntities();
    }
}
