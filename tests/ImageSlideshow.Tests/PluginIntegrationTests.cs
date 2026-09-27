using MacroDeck.Plugin.Testing;
using NUnit.Framework;
using SkiaSharp;

namespace ImageSlideshow.Tests;

/// <summary>
/// Behaviour tests through <see cref="PluginTestHarness"/>: the plugin's own capability handlers run,
/// but nothing crosses a socket. This is where you test what your integration does.
/// </summary>
[TestFixture]
public sealed class PluginIntegrationTests
{
	private static PluginTestHarness CreateHarness() =>
		PluginTestHarness.Create(builder => builder
			.UseLocalization(Strings.LocalizationCatalog)
			.RegisterIntegration<PluginIntegration>());

	[Test]
	public async Task The_plugin_builds_and_initializes()
	{
		await using var harness = CreateHarness();

		Assert.DoesNotThrowAsync(harness.InitializeIntegrationsAsync);
	}

	[Test]
	public async Task The_slideshow_action_advances_a_readable_folder()
	{
		await using var harness = CreateHarness();
		await harness.InitializeIntegrationsAsync();
		var folder = Path.Combine(Path.GetTempPath(), $"slideshow-test-{Guid.NewGuid():N}");
		Directory.CreateDirectory(folder);
		try
		{
			File.WriteAllBytes(Path.Combine(folder, "example.png"), [0x89]);
			var outcome = await harness.Actions.ExecuteAsync(
				"image-slideshow",
				new Dictionary<string, object?> { ["folder"] = folder, ["intervalSeconds"] = 5d, ["imageSize"] = 256d });
			Assert.That(outcome.Succeeded, Is.True);

			var action = new ImageSlideshowAction();
			var first = await action.GetActionIconAsync(new Dictionary<string, object?>
				{ ["folder"] = folder, ["intervalSeconds"] = 5d, ["imageSize"] = 256d }, CancellationToken.None);
			var repeat = await action.GetActionIconAsync(new Dictionary<string, object?>
				{ ["folder"] = folder, ["intervalSeconds"] = 5d, ["imageSize"] = 256d }, CancellationToken.None);
			var other = await action.GetActionIconAsync(new Dictionary<string, object?>
				{ ["folder"] = folder, ["intervalSeconds"] = 60d, ["imageSize"] = 128d }, CancellationToken.None);
			Assert.That(first?.Version, Is.EqualTo(repeat?.Version));
			Assert.That(other?.Version, Is.Not.EqualTo(first?.Version));
		}
		finally
		{
			Directory.Delete(folder, recursive: true);
		}
	}

	[Test]
	public async Task The_slideshow_action_requires_a_folder()
	{
		await using var harness = CreateHarness();
		await harness.InitializeIntegrationsAsync();

		var outcome = await harness.Actions.ExecuteAsync(
			"image-slideshow",
			new Dictionary<string, object?> { ["folder"] = "   " });

		Assert.That(outcome.Succeeded, Is.False);
	}

	[Test]
	public async Task A_real_image_is_rendered_without_an_external_converter()
	{
		var folder = Path.Combine(Path.GetTempPath(), $"slideshow-render-{Guid.NewGuid():N}");
		Directory.CreateDirectory(folder);
		try
		{
			var file = Path.Combine(folder, "wide.png");
			using (var surface = SKSurface.Create(new SKImageInfo(200, 100)))
			{
				surface.Canvas.Clear(SKColors.CornflowerBlue);
				using var image = surface.Snapshot();
				using var data = image.Encode(SKEncodedImageFormat.Png, 100);
				using var output = File.Create(file);
				data.SaveTo(output);
			}

			var action = new ImageSlideshowAction();
			var parameters = new Dictionary<string, object?>
			{
				["folder"] = folder, ["intervalSeconds"] = 10, ["imageSize"] = 128
			};
			var snapshot = await action.GetActionIconAsync(parameters, CancellationToken.None);
			Assert.That(snapshot, Is.Not.Null);
			var content = await action.GetActionIconContentAsync(parameters, snapshot!.Version, CancellationToken.None);
			Assert.That(content, Is.Not.Null);
			using var rendered = SKBitmap.Decode(content!.Data);
			Assert.That(rendered, Is.Not.Null);
			Assert.Multiple(() =>
			{
				Assert.That(rendered!.Width, Is.EqualTo(128));
				Assert.That(rendered.Height, Is.EqualTo(128));
			});
		}
		finally
		{
			Directory.Delete(folder, recursive: true);
		}
	}

	[TestCase(0, 512)]
	[TestCase(3601, 512)]
	[TestCase(10, 63)]
	[TestCase(10, 1025)]
	[TestCase(1.5, 512)]
	public async Task Invalid_interval_or_size_is_rejected(double interval, double size)
	{
		await using var harness = CreateHarness();
		await harness.InitializeIntegrationsAsync();
		var outcome = await harness.Actions.ExecuteAsync("image-slideshow",
			new Dictionary<string, object?>
			{
				["folder"] = Path.GetTempPath(), ["intervalSeconds"] = interval, ["imageSize"] = size
			});
		Assert.That(outcome.Succeeded, Is.False);
	}
}

/// <summary>
/// The localization set is generated from <c>Localization/*.resx</c>, so these guard the wiring rather
/// than any wording: a missing catalog registration leaves every label showing its raw key.
/// </summary>
[TestFixture]
public sealed class LocalizationTests
{
	[Test]
	public void The_catalog_is_scoped_to_the_plugin_id()
	{
		Assert.That(Strings.LocalizationCatalog.Scope, Is.EqualTo("plugin:com.yussefabdelwahab.image-slideshow"));
	}

	[Test]
	public void English_is_the_default_culture()
	{
		Assert.That(Strings.LocalizationCatalog.DefaultCulture, Is.EqualTo("en"));
		Assert.That(Strings.LocalizationCatalog.Cultures, Does.Contain("en"));
	}

	[Test]
	public void The_action_strings_come_from_the_catalog()
	{
		Assert.That(Strings.LocalizationCatalog.KeysOf("en"), Does.Contain("Actions.ImageSlideshow.Name"));
	}

	[Test]
	public void Every_key_the_default_culture_declares_resolves_to_text()
	{
		foreach (var key in Strings.LocalizationCatalog.KeysOf("en"))
		{
			Assert.That(Strings.LocalizationCatalog.TryGetTemplate("en", key, out var text), Is.True);
			Assert.That(text, Is.Not.Empty);
		}
	}
}
