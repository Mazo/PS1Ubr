using System;
using System.Collections.Generic;
using System.Linq;
using AmenityExtractor.Models;

namespace AmenityExtractor
{
    /// <summary>
    /// Ownership of ground-cover objects in the caverns, as the server needs it in the exported zonemap. Applied after GUIDs are assigned (it only changes
    /// Owner and IsChildObject, which the client ordering does not use).
    ///
    /// The cave ground-cover files list some objects with a "!" prefix ("part of a building") without saying which building. The server loads building
    /// amenities through Owner and every other object as free-standing, so these objects need an explicit decision. The client's own link for these objects
    /// is not captured for caves, so the rules below are not client-derived (inferred, not confirmed): they reproduce the committed zonemaps, in which
    /// the owners were chosen by hand (vanu_equipment_term: 174 objects, crystals: 317 objects).
    ///  - crystals_* objects are free-standing: no owner, not a child.
    ///  - a vanu_equipment_term that no building model contains is owned by the redoubt it stands in (within RedoubtRadius, observed 12 to 16 units, nearest
    ///    other redoubt at 64 or more) and otherwise by the cave's vanu_core.
    /// </summary>
    public static class CaveExportRules
    {
        private const double RedoubtRadius = 20.0;

        public static void Apply(List<PlanetSideObject> objects)
        {
            foreach (var crystal in objects.Where(o => o.ObjectType.StartsWith("crystals_", StringComparison.Ordinal)))
            {
                crystal.Owner = null;
                crystal.IsChildObject = false;
            }

            var redoubts = objects.Where(o => o.ObjectType == "redoubt").ToList();
            var cores = objects.Where(o => o.ObjectType == "vanu_core").ToList();

            foreach (var terminal in objects.Where(o => o.ObjectType == "vanu_equipment_term" && o.IsChildObject && o.Owner == null))
            {
                var redoubt = Nearest(terminal, redoubts);
                if (redoubt != null && Distance(terminal, redoubt) <= RedoubtRadius)
                {
                    terminal.Owner = redoubt.Id;
                    continue;
                }

                var core = Nearest(terminal, cores);
                if (core != null) terminal.Owner = core.Id;
            }
        }

        private static PlanetSideObject Nearest(PlanetSideObject from, List<PlanetSideObject> candidates)
        {
            return candidates.OrderBy(c => Distance(from, c)).FirstOrDefault();
        }

        private static double Distance(PlanetSideObject a, PlanetSideObject b)
        {
            var dx = a.AbsX - b.AbsX;
            var dy = a.AbsY - b.AbsY;
            var dz = a.AbsZ - b.AbsZ;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }
}
