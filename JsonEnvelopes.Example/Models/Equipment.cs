using System;
using System.Collections.Generic;
using System.Text;

namespace JsonEnvelopes.Example.Models
{
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
