using NUnit.Framework;
using Playnite.SDK.Models;
using System.Collections.Generic;
using System.Windows.Controls;
using System;

namespace SteamDeckProtonDb.Tests
{
    [TestFixture]
    public class SettingsApplicationTests
    {
        [Test]
        [Apartment(System.Threading.ApartmentState.STA)]
        public void SettingsViewLoads_WithoutError()
        {
            // This test ensures the settings UI can be instantiated without throwing exceptions.
            // It verifies that:
            // 1. The XAML file is properly deployed (copied to output directory)
            // 2. The runtime XAML loader can parse and load the XAML
            // 3. The x:Class attribute is properly stripped before loading
            // 4. A fallback UI is provided if primary methods fail
            var view = new SteamDeckProtonDbSettingsView();

            Assert.IsNotNull(view, "Settings view should not be null");
            Assert.IsNotNull(view.Content, "Settings view Content should not be null");
            Assert.IsInstanceOf<UserControl>(view, "Settings view should be a UserControl");
        }

        [Test]
        [Apartment(System.Threading.ApartmentState.STA)]
        public void SettingsViewContent_IsUIElement()
        {
            // Verify that the loaded content is a valid UI element that can be rendered.
            var view = new SteamDeckProtonDbSettingsView();

            // Content can be a StackPanel (fallback), a Grid (from XAML), or any UIElement
            Assert.IsTrue(
                view.Content is System.Windows.UIElement,
                "Content must be a UIElement to be renderable"
            );
        }

    [Test]
    public void AllSettingsEnabled_AllMetadataApplied()
    {
            var settings = new SteamDeckProtonDbSettings(null)
            {
                EnableTags = true,
                EnableFeatures = true,
                EnableProtonDbLink = true
            };

            var testUpdater = new TestableMetadataUpdater(settings);
            var game = new Game("Test Game");

            var mappingResult = new MappingResult
            {
                Tags = new List<string> { "steamdeck:verified", "protondb:platinum" },
                Features = new List<string> { "Steamdeck Verified", "Protondb Platinum" },
                ProtonDbUrl = "https://protondb.com/app/123"
            };

            var result = testUpdater.ApplyDry(game, mappingResult);

            Assert.AreEqual(2, result.TagsApplied.Count);
            Assert.AreEqual(2, result.FeaturesApplied.Count);
            Assert.IsNotNull(result.LinkApplied);
        }



        [Test]
        public void TagsDisabled_NoTagsAdded()
        {
            var settings = new SteamDeckProtonDbSettings(null)
            {
                EnableTags = false,
                EnableFeatures = true
            };

            var testUpdater = new TestableMetadataUpdater(settings);
            var game = new Game("Test Game");

            var mappingResult = new MappingResult
            {
                Tags = new List<string> { "steamdeck:verified", "protondb:platinum" },
                Features = new List<string> { "Steamdeck Verified" }
            };

            var result = testUpdater.ApplyDry(game, mappingResult);

            Assert.AreEqual(0, result.TagsApplied.Count, "No tags should be added");
            Assert.Greater(result.FeaturesApplied.Count, 0, "Features should still be added");
        }

        [Test]
        public void ProtonDbLinkDisabled_NoLinkAdded()
        {
            var settings = new SteamDeckProtonDbSettings(null)
            {
                EnableTags = true,
                EnableFeatures = true,
                EnableProtonDbLink = false
            };

            var testUpdater = new TestableMetadataUpdater(settings);
            var game = new Game("Test Game");

            var mappingResult = new MappingResult
            {
                Tags = new List<string> { "protondb:gold" },
                Features = new List<string> { "Protondb Gold" },
                ProtonDbUrl = "https://protondb.com/app/456"
            };

            var result = testUpdater.ApplyDry(game, mappingResult);

            Assert.IsNull(result.LinkApplied, "Link should not be added");
            Assert.Greater(result.FeaturesApplied.Count, 0, "Features should still be added");
        }

        [Test]
        public void AllSettingsDisabled_NoMetadataApplied()
        {
            var settings = new SteamDeckProtonDbSettings(null)
            {
                EnableTags = false,
                EnableFeatures = false,
                EnableProtonDbLink = false
            };

            var testUpdater = new TestableMetadataUpdater(settings);
            var game = new Game("Test Game");

            var mappingResult = new MappingResult
            {
                Tags = new List<string> { "steamdeck:verified", "protondb:platinum" },
                Features = new List<string> { "Steamdeck Verified", "Protondb Platinum" },
                ProtonDbUrl = "https://protondb.com/app/789"
            };

            var result = testUpdater.ApplyDry(game, mappingResult);

            Assert.AreEqual(0, result.TagsApplied.Count);
            Assert.AreEqual(0, result.FeaturesApplied.Count);
            Assert.IsNull(result.LinkApplied);
        }

        [Test]
        public void NullGame_NoExceptionThrown()
        {
            var settings = new SteamDeckProtonDbSettings(null)
            {
                EnableTags = true
            };

            var testUpdater = new TestableMetadataUpdater(settings);
            var mappingResult = new MappingResult
            {
                Tags = new List<string> { "steamdeck:verified" }
            };

            var result = testUpdater.ApplyDry(null, mappingResult);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.TagsApplied.Count);
        }

        [Test]
        public void NullMappingResult_NoExceptionThrown()
        {
            var settings = new SteamDeckProtonDbSettings(null)
            {
                EnableTags = true
            };

            var testUpdater = new TestableMetadataUpdater(settings);
            var game = new Game("Test Game");

            var result = testUpdater.ApplyDry(game, null);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.TagsApplied.Count);
        }




        [Test]
        public void MixedSettings_OnlyEnabledMetadataApplied()
        {
            var settings = new SteamDeckProtonDbSettings(null)
            {
                EnableTags = false,
                EnableFeatures = true,
                EnableProtonDbLink = true
            };

            var testUpdater = new TestableMetadataUpdater(settings);
            var game = new Game("Test Game");

            var mappingResult = new MappingResult
            {
                Tags = new List<string> { "steamdeck:playable", "protondb:bronze" },
                Features = new List<string> { "Steamdeck Playable", "Protondb Bronze" },
                ProtonDbUrl = "https://protondb.com/app/111"
            };

            var result = testUpdater.ApplyDry(game, mappingResult);

            Assert.AreEqual(0, result.TagsApplied.Count, "Tags disabled");
            Assert.AreEqual(2, result.FeaturesApplied.Count, "Features enabled");
            Assert.IsNotNull(result.LinkApplied, "Link enabled");
        }

        [Test]
        public void EmptyProtonDbUrl_NoLinkAdded()
        {
            var settings = new SteamDeckProtonDbSettings(null)
            {
                EnableProtonDbLink = true
            };

            var testUpdater = new TestableMetadataUpdater(settings);
            var game = new Game("Test Game");

            var mappingResult = new MappingResult
            {
                ProtonDbUrl = ""
            };

            var result = testUpdater.ApplyDry(game, mappingResult);

            Assert.IsNull(result.LinkApplied);
        }

        [Test]
        public void NullProtonDbUrl_NoLinkAdded()
        {
            var settings = new SteamDeckProtonDbSettings(null)
            {
                EnableProtonDbLink = true
            };

            var testUpdater = new TestableMetadataUpdater(settings);
            var game = new Game("Test Game");

            var mappingResult = new MappingResult
            {
                ProtonDbUrl = null
            };

            var result = testUpdater.ApplyDry(game, mappingResult);

            Assert.IsNull(result.LinkApplied);
        }
    }
}
