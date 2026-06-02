using System.ComponentModel.DataAnnotations;
using SqlSugar;

namespace EveConv.RdbStorage;

public abstract class BaseEntity
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true, ColumnName = "id")]
    public int Id { get; set; }
    
    [SugarColumn(IsNullable = false, ColumnName = "created_at")]
    public DateTime CreatedAt { get; set; }
    [SugarColumn(IsNullable = false, ColumnName = "updated_at")]
    public DateTime UpdatedAt { get; set; }
}
