using SqlSugar;

namespace EveConv.RdbStorage.Tests;

[SugarTable("test")]
[SugarIndex("test_key", nameof(Key), OrderByType.Asc, true)]
public class TestEntity : BaseEntity
{
    [SugarColumn(IsNullable = false)]
    public string Key { get; set; } = string.Empty;

    public string? Value { get; set; }
}
