using Alerta.Core;

namespace Alerta.Tests;

public class ExerciseCatalogTests
{
    [Fact]
    public void Default_catalog_has_at_least_eight_complete_exercises()
    {
        Assert.True(ExerciseCatalog.Default.Count >= 8);
        Assert.All(ExerciseCatalog.Default, e =>
        {
            Assert.False(string.IsNullOrWhiteSpace(e.Title));
            Assert.False(string.IsNullOrWhiteSpace(e.Instructions));
        });
    }

    [Fact]
    public void Next_never_repeats_the_previous_exercise()
    {
        var catalog = new ExerciseCatalog(random: new Random(42));
        var previous = catalog.Next();

        for (var i = 0; i < 1000; i++)
        {
            var current = catalog.Next();
            Assert.NotEqual(previous, current);
            previous = current;
        }
    }

    [Fact]
    public void Single_item_catalog_returns_that_item_repeatedly()
    {
        var only = new Exercise("A", "B");
        var catalog = new ExerciseCatalog([only]);

        Assert.Equal(only, catalog.Next());
        Assert.Equal(only, catalog.Next());
    }

    [Fact]
    public void Empty_catalog_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new ExerciseCatalog([]));
    }
}
