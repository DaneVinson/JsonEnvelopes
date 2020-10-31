using JsonEnvelopes.Example.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace JsonEnvelopes.Example.Commands
{
    public class CreateCharacter
    {
        public int[]? Abilities { get; set; }
        public string? Alignment { get; set; }
        public DateTime BirthDate { get; set; }
        public string? Class { get; set; }
        public string? CampaignName { get; set; }
        public string? CharacterName { get; set; }
        public string? DungeonMaster { get; set; }
        public string? Name { get; set; }
        public Guid PlayerId { get; set; }
        public string? Race { get; set; }
        public List<Equipment> StartingEquipment { get; set; } = new List<Equipment>();
        public int StartingHitPoints { get; set; }
        public int StartingLevel { get; set; } = 1;
        public int StartingExperiencePoints { get; set; }

        public override string ToString()
        {
            return $"Create {Name}, Class: {Class}, Race: {Race}";
        }
    }
}
