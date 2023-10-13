namespace Ryujinx.Common.Collections
{
    public interface IIntrusiveListNode<T> where T : class
    {
        T ListPrevious { get; set; }
        T ListNext { get; set; }
    }
}