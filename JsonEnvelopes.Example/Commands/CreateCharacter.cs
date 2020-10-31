using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace JsonEnvelopes.Example.Commands
{
    public class CreateCharacter : ICommand, IRequest<bool>
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
            return $"Create {Name}, {Race} {Class}";
        }
    }

    public class Equipment
    {
        public Equipment()
        { }

        public Equipment(string name, string notes = "", int quantity = 1) =>
            (Name, Notes, Quantity) = (name, notes, quantity);

        public int Encumbrance { get; set; }
        public string? Name { get; set; }
        public string? Notes { get; set; }
        public int Quantity { get; set; } = 1;
    }
}
