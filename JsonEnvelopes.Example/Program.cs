using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;

namespace JsonEnvelopes.Example
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                var stopwatch = new Stopwatch();
                var iterations = 100000;

                var castFireballEnvelope = new Envelope<CastFireball>(new CastFireball("Mordenkainen", "x:80, y:20, z:0", 5));
                var createCharacterEnvelope = new Envelope<CreateCharacter>(GetCreateCharacterCommand());

                var json = JsonSerializer.Serialize<Envelope>(createCharacterEnvelope);

                for (int j = 0; j < 3; j++)
                {
                    stopwatch.Restart();
                    for (int i = 0; i < iterations; i++)
                    {
                        //var jsonEnvelope = JsonSerializer.Serialize<Envelope>(createCharacterEnvelope);
                        var resultEnvelope = JsonSerializer.Deserialize<Envelope>(json);
                    }
                    stopwatch.Stop();
                    //Console.WriteLine($"Serialize x {iterations}: {stopwatch.ElapsedMilliseconds} ms");
                    Console.WriteLine($"Deserialize x {iterations}: {stopwatch.ElapsedMilliseconds} ms");
                }

                //var resultCommand = resultEnvelope.GetContent();
            }
            catch (Exception ex)
            {
                Console.WriteLine("{0} - {1}", ex.GetType(), ex.Message);
                Console.WriteLine(ex.StackTrace ?? String.Empty);
            }
            finally
            {
                Console.WriteLine();
                Console.WriteLine("...");
                Console.ReadKey();
            }
        }

        private static CreateCharacter GetCreateCharacterCommand()
        {
            return new CreateCharacter()
            {
                Abilities = new int[] { 8, 12, 8, 14, 10, 19 },
                Alignment = "Neutral Good",
                BirthDate = DateTime.Today.AddYears(-40),
                CampaignName = "Weatherstorm",
                CharacterName = "Sandren Light Fingers",
                Class = "Bard",
                DungeonMaster = "Bilbo Baggins",
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
    }
}
