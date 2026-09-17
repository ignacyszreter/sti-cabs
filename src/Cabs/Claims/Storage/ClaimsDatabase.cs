using System.Data.Common;
using Microsoft.Data.Sqlite;

namespace LegacyFighter.Cabs.Claims.Storage;

public sealed class ClaimsDatabase : IDisposable
{
  public ClaimsDatabase()
  {
    var connection = new SqliteConnection("Filename=:memory:");
    connection.Open();
    Connection = connection;
  }

  public DbConnection Connection { get; }

  public void Dispose()
  {
    Connection.Dispose();
  }
}
