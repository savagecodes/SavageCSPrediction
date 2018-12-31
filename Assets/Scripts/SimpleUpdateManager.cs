using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class SimpleUpdateManager : MonoBehaviour {

    public static SimpleUpdateManager instance;

    Dictionary<int, Action> _prePhysicsUpdatersByID = new Dictionary<int, Action>();
    Dictionary<int, Action> _physicsUpdatersByID = new Dictionary<int, Action>();
    Dictionary<int, Action> _afterPhysicsUpdatersByID = new Dictionary<int, Action>();

    float _timer;

    public uint tickNumber;

    private void Awake()
    {
        if (instance != null) instance = null;
        instance = this;
    }

    public void RegisterPrePhysicsUpdater(int iD,Action update)
    {
        if (!_prePhysicsUpdatersByID.ContainsKey(iD))
        {
            _prePhysicsUpdatersByID.Add(iD, update);
        }

        _prePhysicsUpdatersByID[iD] += update; 
    }

    public void DeRegisterPrePhysicsUpdater(int iD, Action update)
    {
        if (!_prePhysicsUpdatersByID.ContainsKey(iD))
        {
            return;
        }

        _prePhysicsUpdatersByID[iD] -= update;
    }

    public void RegistePhysicsUpdater(int iD, Action update)
    {
        if (!_physicsUpdatersByID.ContainsKey(iD))
        {
            _physicsUpdatersByID.Add(iD, update);
        }

        _physicsUpdatersByID[iD] += update;
    }

    public void DeRegisterPhysicsUpdater(int iD, Action update)
    {
        if (!_physicsUpdatersByID.ContainsKey(iD))
        {
            return;
        }

        _physicsUpdatersByID[iD] -= update;
    }

    public void RegisterAfterPhysicsUpdater(int iD, Action update)
    {
        if (!_afterPhysicsUpdatersByID.ContainsKey(iD))
        {
            _afterPhysicsUpdatersByID.Add(iD, update);
        }

        _afterPhysicsUpdatersByID[iD] += update;
    }

    public void DeRegisterAfterPhysicsUpdater(int iD, Action update)
    {
        if (!_afterPhysicsUpdatersByID.ContainsKey(iD))
        {
            return;
        }

        _afterPhysicsUpdatersByID[iD] -= update;
    }

    // Update is called once per frame
    void Update () {

        _timer += Time.deltaTime;

        ExecuteUpdater(_prePhysicsUpdatersByID);

        while (_timer >= Time.fixedDeltaTime)
        {
            _timer -= Time.fixedDeltaTime;
            ExecuteUpdater(_physicsUpdatersByID);
            Physics.Simulate(Time.fixedDeltaTime);
        }

        ExecuteUpdater(_afterPhysicsUpdatersByID);

    }

    void ExecuteUpdater(Dictionary<int,Action> updater)
    {
        foreach (var update in updater)
        {
            update.Value();
        }
    }

}
