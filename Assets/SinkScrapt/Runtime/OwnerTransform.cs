using Unity.Netcode.Components;
namespace SinkScrapt
{
    public class OwnerTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative() => false;
    }
}
