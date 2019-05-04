using System.Collections.Generic;
using UnityEngine;

public class StateConfigurer<T>
    {
        State<T> instance;
        Dictionary<T, Transition<T>> transitions = new Dictionary<T, Transition<T>>();

        public StateConfigurer(State<T> state)
        {
            instance = state;
        }

        public StateConfigurer<T> SetTransition(T input, State<T> target)
        {
            transitions.Add(input, new Transition<T>(input, target));
            return this;
        }

        public void Done()
        {
            instance.Configure(transitions);
        }
    }

    public static class StateConfigurer
    {
        public static StateConfigurer<T> New<T>(State<T> state)
        {
            return new StateConfigurer<T>(state);
        }
    
}