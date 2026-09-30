using System.IO;
using System.Text.Json;
using NUnit.Framework;
using PersonalArena.Core.Survivor;

namespace PersonalArena.Core.Tests.Survivor
{
    public sealed class SurvivorObservationSchemaTests
    {
        [Test]
        public void JsonSchemaMatchesTheCoreLayout()
        {
            string path = Path.Combine(FindRepoRoot(), "Trainer", "schemas", "survivor_v4.json");
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            JsonElement root = document.RootElement;

            Assert.That(root.GetProperty("schema_version").GetInt32(), Is.EqualTo(SurvivorObservation.SchemaVersion));
            int offset = 0;
            int total = 0;
            foreach (JsonElement segment in root.GetProperty("observation").EnumerateArray())
            {
                string name = segment.GetProperty("name").GetString();
                int repeat = segment.GetProperty("repeat").GetInt32();
                int fieldCount = segment.GetProperty("fields").GetArrayLength();
                Assert.That(offset, Is.EqualTo(ExpectedOffset(name)), name);
                if (name == "rays") Assert.That(fieldCount, Is.EqualTo(SurvivorObservation.RayStride));
                int size = repeat * fieldCount;
                offset += size;
                total += size;
            }

            Assert.That(total, Is.EqualTo(SurvivorObservation.Size));
            Assert.That(root.GetProperty("observation_size").GetInt32(), Is.EqualTo(total));

            int actionIndex = 0;
            foreach (JsonElement action in root.GetProperty("actions").EnumerateArray())
            {
                int expected = actionIndex switch
                {
                    0 => SurvivorInput.MoveBranchSize,
                    1 => SurvivorInput.SkillBranchSize,
                    2 => SurvivorInput.PickBranchSize,
                    _ => -1
                };
                Assert.That(action.GetProperty("size").GetInt32(), Is.EqualTo(expected));
                actionIndex++;
            }
            Assert.That(actionIndex, Is.EqualTo(3));
        }

        private static int ExpectedOffset(string name)
        {
            return name switch
            {
                "self" => SurvivorObservation.SelfOffset,
                "inventory" => SurvivorObservation.InventoryOffset,
                "offers" => SurvivorObservation.OffersOffset,
                "rays" => SurvivorObservation.RaysOffset,
                "density" => SurvivorObservation.DensityOffset,
                _ => -1
            };
        }

        internal static string FindRepoRoot()
        {
            DirectoryInfo directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))) return directory.FullName;
                directory = directory.Parent;
            }
            throw new DirectoryNotFoundException("Could not find the Personal Arena repository root.");
        }
    }
}
