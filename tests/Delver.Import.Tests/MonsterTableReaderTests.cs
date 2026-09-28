using Delver.Import.Normalized;
using Xunit;

namespace Delver.Import.Tests;

public class MonsterTableReaderTests
{
    [Fact]
    public void Read_MapsMonstersCategoryArrays()
    {
        // Donor: data/monsters.dat — {"monsters": {"CAVE": [ {…} ]}}
        const string donor = """
            {
                "monsters": {
                    "CAVE": [
                        {
                            "class": "com.interrupt.dungeoneer.entities.Monster",
                            "tex": 32,
                            "name": "SPIDER",
                            "speed": 0.009,
                            "maxHp": 4,
                            "atk": 4,
                            "baseLevel": 1,
                            "scale": 1.5,
                            "alertSound": "mobs/spider/en_spider_alert_01.mp3,mobs/spider/en_spider_alert_02.mp3",
                            "walkSound": "mobs/spider/en_spider_walk.mp3"
                        }
                    ]
                }
            }
            """;

        NormalizedMonster monster = Assert.Single(MonsterTableReader.Read(donor));

        Assert.Equal("CAVE", monster.Category);
        Assert.Equal("SPIDER", monster.Name);
        Assert.Equal("com.interrupt.dungeoneer.entities.Monster", monster.Class);
        Assert.Equal(32, monster.SpriteTex);
        Assert.Equal(4, monster.Hp);
        Assert.Equal(4, monster.Damage);
        Assert.Equal(1, monster.Level);
        Assert.Equal(1.5, monster.Scale);
        Assert.Equal(2, monster.Sounds.Alert.Count);
        Assert.Equal("mobs/spider/en_spider_alert_01.mp3", monster.Sounds.Alert[0]);
        Assert.Single(monster.Sounds.Walk);
    }

    [Fact]
    public void Read_MapsEntityNameMaps()
    {
        // Donor: data/entities.dat — {"entities": {"Ambient Sounds": {"Lava": {…}}}}
        const string donor = """
            {
                "entities": {
                    "Ambient Sounds": {
                        "Lava": {
                            "class": "com.interrupt.dungeoneer.entities.AmbientSound",
                            "volume": 0.8,
                            "soundFile": "/ambient/env_lava.mp3"
                        }
                    }
                }
            }
            """;

        NormalizedMonster row = Assert.Single(MonsterTableReader.Read(donor));

        Assert.Equal("Ambient Sounds", row.Category);
        Assert.Equal("Lava", row.Name);
        Assert.Equal("com.interrupt.dungeoneer.entities.AmbientSound", row.Class);
        Assert.Null(row.Hp);
    }
}
