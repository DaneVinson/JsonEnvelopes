using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace JsonEnvelopes.Example.Commands
{
    /// <summary>
    /// A command to cast a Fireball spell.
    /// </summary>
    public class CastFireball
    {
        public CastFireball()
        { }

        public CastFireball(string spellcaster, string location, int level = 3) =>
            (Spellcaster, Location, Level) = (spellcaster, location, level);

        public int Level { get; set; }
        public string? Location { get; set; }
        public string? Spellcaster { get; set; }

        public override string ToString() =>
            $"{Spellcaster} casts a level {Level} Fireball at {Location}";
    }
}
