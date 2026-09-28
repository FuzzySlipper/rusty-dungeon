using Delver.Import.Normalized;
using Xunit;

namespace Delver.Import.Tests;

public class ItemTableReaderTests
{
    [Fact]
    public void Read_MapsItemCategoryArrays()
    {
        // Donor: data/items.dat — top-level category arrays ("unique", "melee", …).
        const string donor = """
            {
                "unique": [
                    {
                        "class": "com.interrupt.dungeoneer.entities.items.Sword",
                        "itemType": "sword",
                        "tex": 9,
                        "name": "Lucky Dagger",
                        "baseDamage": 1,
                        "randDamage": 5,
                        "cost": 1000,
                        "minItemLevel": 1,
                        "maxItemLevel": 5
                    }
                ],
                "melee": []
            }
            """;

        NormalizedItem item = Assert.Single(ItemTableReader.Read(donor));

        Assert.Equal("unique", item.Category);
        Assert.Equal("Lucky Dagger", item.Name);
        Assert.Equal("com.interrupt.dungeoneer.entities.items.Sword", item.Class);
        Assert.Equal("sword", item.ItemType);
        Assert.Equal(9, item.SpriteTex);
        Assert.Equal(1000, item.Cost);
        Assert.Equal(1, item.BaseDamage);
        Assert.Equal(5, item.RandDamage);
        Assert.Equal(1, item.MinItemLevel);
        Assert.Equal(5, item.MaxItemLevel);
    }

    [Fact]
    public void Read_SkipsNonObjectEntries()
    {
        NormalizedItem item = Assert.Single(ItemTableReader.Read(
            "{\"food\": [{\"name\": \"Apple\"}], \"messages\": [[\"a note\"]]}"));

        Assert.Equal("food", item.Category);
        Assert.Equal("Apple", item.Name);
    }
}
