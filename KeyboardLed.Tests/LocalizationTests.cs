using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using KeyboardLed.Services;
using Xunit;

namespace KeyboardLed.Tests
{
    public class LocalizationTests
    {
        [Fact]
        public void Both_languages_have_the_same_keys()
        {
            var missingInHungarian = Strings.English.Keys.Except(Strings.Hungarian.Keys).ToList();
            var missingInEnglish = Strings.Hungarian.Keys.Except(Strings.English.Keys).ToList();

            Assert.True(missingInHungarian.Count == 0, "Missing in Hungarian: " + string.Join(", ", missingInHungarian));
            Assert.True(missingInEnglish.Count == 0, "Missing in English: " + string.Join(", ", missingInEnglish));
        }

        [Fact]
        public void Every_key_used_in_source_exists()
        {
            var sourceDir = Path.Combine(FindRepoRoot(), "KeyboardLed");
            var used = new HashSet<string>();

            var patterns = new[]
            {
                new Regex(@"\{l:T (\w+)\}"),                                   // XAML: {l:T Key}
                new Regex(@"Loc\.(?:T|Format)\(""(\w+)"""),                      // Loc.T("Key")
                new Regex(@"new Option<\w+>\([^,]+,\s*""(\w+)""\)"),             // Option(value, "Key")
                new Regex(@"\(""#[0-9A-Fa-f]{6}"",\s*""(\w+)""\)"),               // ("#hex", "Key")
                new Regex(@"Loc\.T\(\w+ \? ""(\w+)"" : ""(\w+)""\)"),            // Loc.T(x ? "A" : "B")
                new Regex(@"Loc\.T\(on \? ""(\w+)"" : ""(\w+)""\)")
            };

            foreach (var file in Directory.EnumerateFiles(sourceDir, "*.*", SearchOption.AllDirectories)
                         .Where(f => (f.EndsWith(".cs") || f.EndsWith(".xaml")) && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")))
            {
                var text = File.ReadAllText(file);
                foreach (var pattern in patterns)
                {
                    foreach (Match match in pattern.Matches(text))
                    {
                        for (int g = 1; g < match.Groups.Count; g++)
                        {
                            if (match.Groups[g].Success) used.Add(match.Groups[g].Value);
                        }
                    }
                }
            }

            Assert.NotEmpty(used);
            var missing = used.Where(k => !Strings.English.ContainsKey(k)).OrderBy(k => k).ToList();
            Assert.True(missing.Count == 0, "Missing string keys: " + string.Join(", ", missing));
        }

        [Fact]
        public void Format_strings_have_matching_placeholders()
        {
            foreach (var (key, english) in Strings.English)
            {
                var hungarian = Strings.Hungarian[key];
                Assert.Equal(Placeholders(english), Placeholders(hungarian));
            }
        }

        private static string Placeholders(string s) =>
            string.Join(",", Regex.Matches(s, @"\{\d+\}").Select(m => m.Value).OrderBy(v => v));

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "KeyboardLed.sln")))
            {
                dir = dir.Parent;
            }
            return dir?.FullName ?? throw new InvalidOperationException("Repository root not found");
        }
    }
}
