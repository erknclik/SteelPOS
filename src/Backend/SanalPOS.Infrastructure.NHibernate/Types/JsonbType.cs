using System.Data.Common;
using NHibernate;
using NHibernate.Engine;
using NHibernate.SqlTypes;
using NHibernate.UserTypes;
using Npgsql;
using NpgsqlTypes;

namespace SanalPOS.Infrastructure.NHibernate.Types;

/// <summary>
/// PostgreSQL jsonb kolonuna string yazabilmek için custom user type.
/// (NHibernate'in varsayılan string tipi text olarak gönderir; PG jsonb kolonuna kabul etmez.)
/// </summary>
public class JsonbType : IUserType
{
    public SqlType[] SqlTypes => [new SqlType(System.Data.DbType.String)];
    public Type ReturnedType => typeof(string);
    public bool IsMutable => false;

    public object? NullSafeGet(DbDataReader rs, string[] names, ISessionImplementor session, object owner)
    {
        var ordinal = rs.GetOrdinal(names[0]);
        return rs.IsDBNull(ordinal) ? null : rs.GetString(ordinal);
    }

    public void NullSafeSet(DbCommand cmd, object? value, int index, ISessionImplementor session)
    {
        var parameter = (NpgsqlParameter)cmd.Parameters[index];
        parameter.NpgsqlDbType = NpgsqlDbType.Jsonb;
        parameter.Value = value ?? DBNull.Value;
    }

    public object? DeepCopy(object? value) => value;
    public object? Replace(object? original, object? target, object? owner) => original;
    public object? Assemble(object? cached, object? owner) => cached;
    public object? Disassemble(object? value) => value;
    public new bool Equals(object? x, object? y) => object.Equals(x, y);
    public int GetHashCode(object x) => x.GetHashCode();
}
