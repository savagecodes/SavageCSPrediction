using System;
using System.Collections.Generic;
using UnityEngine;

public class State<T>
    {
        public string Name { get { return name; } }

        public event Action<T> OnEnter = delegate { };
        public event Action OnUpdate = delegate { };
        public event Action<T> OnExit = delegate { };

        string name;
        Dictionary<T, Transition<T>> transitions;

        public State()
        {
        }

        public State(string name)
        {
            this.name = name;
            transitions = new Dictionary<T, Transition<T>>();   //In case of ending state or for use with "any"
        }

        public State<T> Configure(Dictionary<T, Transition<T>> transitions)
        {
            this.transitions = transitions;
            return this;
        }

        public Transition<T> GetTransition(T input)
        {
            return transitions[input];
        }

        public bool Feed(T input, out State<T> next)
        {
       // Debug.Log(name);
            if (transitions.ContainsKey(input))
            {
                var transition = transitions[input];
                transition.OnTransitionExecute(input);
                next = transition.TargetState;
                return true;
            }
            next = this;
            return false;
        }

        public void Enter(T input)
        {
            OnEnter(input);
        }

        public void Update()
        {
            OnUpdate();
        }

        public void Exit(T input)
        {
            OnExit(input);
        }
    
}