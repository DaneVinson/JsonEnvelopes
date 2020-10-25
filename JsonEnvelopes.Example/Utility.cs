using JsonEnvelopes.Example.Commands;
using JsonEnvelopes.Example.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace JsonEnvelopes.Example
{
    public static class Utility
    {
        public static CreateCharacter NewCreateCharacterCommand()
        {
            return new CreateCharacter()
            {
                Abilities = new int[] { 8, 12, 8, 14, 10, 19 },
                Alignment = "Neutral Good",
                BirthDate = DateTime.Today.AddYears(-40),
                CampaignName = "Weatherstorm",
                CharacterName = "Sandren Light Fingers",
                Class = "Bard",
                DungeonMaster = "The DM",
                Name = "Marco the Bard",
                PlayerId = Guid.NewGuid(),
                Race = "Halfling",
                StartingHitPoints = 8,
                StartingLevel = 1,
                StartingExperiencePoints = 0,
                StartingEquipment = new List<Equipment>()
                    {
                        new Equipment("Leather Armor"),
                        new Equipment("Short sword"),
                        new Equipment("Light crossbow"),
                        new Equipment("Crossbow bolts", quantity: 10),
                        new Equipment("Mandolin", "Passed down through 5 generations"),
                        new Equipment("GP", quantity: 8),
                        new Equipment("Normal clothes"),
                        new Equipment("Hat", "Broad brimmed with a large feather"),
                        new Equipment("Backpack"),
                        new Equipment("Waterskin"),
                        new Equipment("Rations"),
                        new Equipment("Bedroll"),
                        new Equipment("Sack"),
                        new Equipment("Flint and steel"),
                        new Equipment("Torches", quantity: 10),
                    }
            };
        }

        public static CastFireball NewCastFireballCommand()
        {
            return new CastFireball("Mordenkainen", "x:80, y:20, z:0", 5);
        }
    }
}
