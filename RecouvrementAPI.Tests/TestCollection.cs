using Xunit;

namespace RecouvrementAPI.Tests
{
    [CollectionDefinition("TestCollection")]
    public class TestCollection : ICollectionFixture<TestWebApplicationFactory>
    {
        // Cette classe n'a pas de code. 
        // Elle sert uniquement de support pour l'attribut [CollectionDefinition] 
        // et pour définir la fixture partagée entre toutes les classes de test du projet.
    }
}
