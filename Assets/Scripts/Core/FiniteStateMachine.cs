
public class FSM<T>
{
    T state;

    public void ChangeState(T state)
    {
        this.state = state;
    }

    public virtual void Enter()
    {
        
    }

    public virtual void Update()
    {
        
    }

    public virtual void Exit()
    {
        
    }
    
    FSM (T state)
    {
        this.state = state;
    }
}
