public interface IMPEG_Module<T>
{
    public void Register(System.Action<T> action, int index);
    public void Unregister(int index);
}