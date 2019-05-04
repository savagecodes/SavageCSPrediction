using System;
using UnityEngine;

    public class EventFSM<T>
    {
        State<T> current;
        public State<T> any;

        public EventFSM(State<T> initial, State<T> any = null)
        {
            current = initial;
            current.Enter(default(T));
            this.any = any != null ? any : new State<T>("<any>");
            this.any.OnEnter += a => { throw new Exception("Can't make transition to fsm's <any> state"); };
        }


        public bool Feed(T input)
        {
            State<T> newState;

            //Added any. Notice the or will not execute the second part if it satisfies the first condition.
            if (current.Feed(input, out newState) || any.Feed(input, out newState))
            {
                current.Exit(input);
                Debug.Log("FSM state: " + current.Name + "---" + input + "---> " + newState.Name);
                current = newState;
                current.Enter(input);
                return true;    //Added return boolean
            }
            return false;
        }

        public State<T> Current { get { return current; } }

        public void Update()
        {
            current.Update();
        }
    
}