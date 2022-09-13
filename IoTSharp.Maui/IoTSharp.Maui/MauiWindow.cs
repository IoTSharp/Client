namespace IoTSharp.Maui;

public class MauiWindow : Window
{
    public MauiWindow() : base() { }

    public MauiWindow(Page page) : base(page) { }


    protected override void OnCreated()
    {
        base.OnCreated();  
    }

    protected override void OnDestroying()
    { 

        base.OnDestroying();
    }
 
}