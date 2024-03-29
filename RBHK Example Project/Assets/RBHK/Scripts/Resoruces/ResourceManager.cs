using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using Unity.VisualScripting;
using UnityEngine;

public class ResourceManager : MonoBehaviour {
    #region Varibles

    public static Dictionary<int, ResourceManager> instances = new Dictionary<int, ResourceManager>();

    [Header("Config")]
    [Tooltip("Id of the player associated with this Resource Management instance.")]
    [SerializeField] private int playerId;

    [Header("Resources")]
    [SerializeField] private Resource[] resources;
    [Space]
    [Tooltip("Period of time between changes to resource supply by demand.")]
    [SerializeField] private float tickTime;
    [SerializeField] private bool updateResourcesOnTick = true;
    [Space]
    [Tooltip("This updates resource between ticks so the player gains resources at a smooth rate.")]
    [SerializeField] private bool continuouslyUpdateResources;

    public delegate void ResourcesChangedCallback(int playerID);
    public static event ResourcesChangedCallback OnResourcesChanged;

    // Resouces are grouped by their tick time
    // First element in value list is reserved for the number of times the tick has been done, This is used in TickUpdate()
    private Dictionary<float, List<int>> resourceTicks = new Dictionary<float, List<int>>();

    private List<ResourceEntry> resourceEntries = new List<ResourceEntry>();

    private float totalDeltaTime = 0;
    private int secondsPassed = 0;

    public int PlayerId { get => playerId; set => playerId = value; }
    public Resource[] Resources { get => resources; set => resources = value; }
    public List<ResourceEntry> ResourceEntries { get => resourceEntries; set => resourceEntries = value; }

    #endregion

    #region Core

    private void Awake() {
        Singleton();
        InstantiateNewLocalResources();
        ConfigureResources();
    }

    private void Singleton() {
        if (instances.ContainsKey(playerId) && instances[playerId] != null) {
            Debug.LogError($"Resource Management Instance Id: ({playerId}) already exists.");
        } else {
            if (instances.ContainsKey(playerId)) instances.Remove(playerId);
            instances.Add(playerId, this);
        }
    }

    private void Update() {
        if (updateResourcesOnTick & !continuouslyUpdateResources) TickUpdate();
        else if (updateResourcesOnTick) ContinuousTickUpdate();
    }

    #endregion

    #region Resources

    // Can't use the Scriptable Objects because those are shared between all instances of the ResourceManager
    private void InstantiateNewLocalResources() {
        // Create list for updated resource entries
        Resource[] _resources = new Resource[resources.Length];

        // Loop through resources
        for (int i = 0; i < resources.Length; i++) {
            Resource newResource = ScriptableObject.CreateInstance<Resource>(); // Create new resource entry

            // Set values of new resource  entry
            newResource.ResourceId = resources[i].ResourceId;
            newResource.ResourceInfo = resources[i].ResourceInfo;
            newResource.Supply = resources[i].Supply;
            newResource.Demand = resources[i].Demand;
            newResource.CustomTickTime = resources[i].CustomTickTime;

            _resources[i] = newResource;
        }

        resources = _resources;
    }

    // Called in Awake
    private void ConfigureResources() {
        // Loop through resources and set custom Tick Time.
        for (int i = 0; i < resources.Length; i++) {
            // Reset supply and demand, because it is a Scriptable Object these are saved
            resources[i].Supply = 0;
            resources[i].Demand = 0;

            // Set TickTime to standard tickTime
            if (resources[i].CustomTickTime == 0)
                resources[i].CustomTickTime = tickTime;

            // If resourceTick already contains specified tickTime, add the index of the current resource to
            if (resourceTicks.ContainsKey(resources[i].CustomTickTime)) {
                resourceTicks[resources[i].CustomTickTime].Add(i);
            } else {
                // Create new resourceTick and allocate first index for ticksPerformed and second for resource index
                resourceTicks.Add(resources[i].CustomTickTime, new List<int>() { 0, i });
            }

        }
    }

    // This updates resource values based on their 
    private void TickUpdate() {
        totalDeltaTime += Time.deltaTime;

        bool resourcesChanged = false;
        foreach (KeyValuePair<float, List<int>> resourceTick in resourceTicks) {
            // Time difference between now and previous tick
            float deltaTimeDifference = totalDeltaTime - (resourceTick.Key * resourceTick.Value[0]);
            // If totalDeltaTime - (tickTime * ticksPerformed) is greater than tickTime, perform another tick
            if (deltaTimeDifference < resourceTick.Key) continue;

            // Run the resource tick the amount of times the tickTime can fit into the deltaTimeDifference
            // This is neccessary because the tickTime might be smaller than deltaTimeDifference, eg. lots of lag or tiny resourceTick
            for (int x = 0; x < Mathf.FloorToInt(deltaTimeDifference % resourceTick.Key); x++) {
                resourceTick.Value[0]++; // Increase ticks performed by 1

                // Loop through resources that should be updated on this tick
                // This starts one 1 because the 0th value is the number of times the tick update has occured
                for (int i = 1; i < resourceTick.Value.Count; i++) {
                    UpdateResourceTick((GameResource)resourceTick.Value[i]); // Update Resource
                }

                resourcesChanged = true;
            }
        }

        if (resourcesChanged & OnResourcesChanged != null) OnResourcesChanged(playerId);
    }

    private void ContinuousTickUpdate() {
        totalDeltaTime += Time.deltaTime;

        // This is here to increase the number of ticks in the resources, could be useful in the future - I'm adding too much complexity
        foreach (KeyValuePair<float, List<int>> resourceTick in resourceTicks) {
            // Time difference between now and previous tick
            float deltaTimeDifference = totalDeltaTime - (resourceTick.Key * resourceTick.Value[0]);
            // If totalDeltaTime - (tickTime * ticksPerformed) is greater than tickTime, perform another tick
            if (deltaTimeDifference < resourceTick.Key) continue;

            // Run the resource tick the amount of times the tickTime can fit into the deltaTimeDifference
            // This is neccessary because the tickTime might be smaller than deltaTimeDifference, eg. lots of lag or tiny resourceTick
            for (int x = 0; x < Mathf.FloorToInt(deltaTimeDifference % resourceTick.Key); x++) {
                resourceTick.Value[0]++; // Increase ticks performed by 1
            }
        }

        if (totalDeltaTime < secondsPassed) return;
        secondsPassed++;

        foreach (KeyValuePair<float, List<int>> resourceTick in resourceTicks) {
            // Loop through resources that should be updated on this tick
            // This starts one 1 because the 0th value is the number of times the tick update has occured
            for (int i = 1; i < resourceTick.Value.Count; i++) {
                resources[resourceTick.Value[i]].Demand = GetResourceDemand((GameResource)resourceTick.Value[i]);
                resources[resourceTick.Value[i]].Supply += resources[resourceTick.Value[i]].Demand / resourceTick.Key;
            }
        }

        if (OnResourcesChanged != null) OnResourcesChanged(playerId);
    }

    #endregion

    #region Utils

    private void UpdateResourceTick(GameResource _id) {
        // Change supply by demand
        resources[(int)_id].Demand = GetResourceDemand(_id);
        resources[(int)_id].Supply += resources[(int)_id].Demand;
    }
    
    public void AddResourceEntry(ResourceEntry _resourceEntry) {
        resourceEntries.Add(_resourceEntry);
        if (_resourceEntry.ChangeOnTick) GetResource(_resourceEntry.ResourceId).Demand = GetResourceDemand(_resourceEntry.ResourceId);
        else GetResource(_resourceEntry.ResourceId).Supply = GetResourceSupply(_resourceEntry.ResourceId);
        if (OnResourcesChanged != null) OnResourcesChanged(playerId);
    }

    public void RemoveResourceEntry(ResourceEntry _resourceEntry) {
        if (resourceEntries.Count <= 0) return;
        if (!resourceEntries.Contains(_resourceEntry)) return;

        resourceEntries.Remove(_resourceEntry);

        if (_resourceEntry.ChangeOnTick) GetResource(_resourceEntry.ResourceId).Demand = GetResourceDemand(_resourceEntry.ResourceId);
        else GetResource(_resourceEntry.ResourceId).Supply = GetResourceSupply(_resourceEntry.ResourceId);
        
        if (OnResourcesChanged != null) OnResourcesChanged(playerId);
    }

    public Resource GetResource(GameResource _resourceID) {
        // Loop through resources and find resource that matches parameter id
        for (int i = 0; i < resources.Length; i++) {
            if (resources[i].ResourceId == _resourceID)
                return resources[i];
        }

        // If no resources found, return Null
        Debug.LogWarning($"Resource of ID: {_resourceID} cannot be found.");
        return null;
    }

    // Use with extreme care (Best to reset all tiles - and structures - before using this)
    public void ResetResources() {
        for (int i = 0; i < resources.Length; i++) {
            resources[i].Supply = 0;
            resources[i].Demand = 0;
        }
    }

    // Use with extreme care (Best to reset all tiles - and structures - before using this)
    public void ResetResourceEntries() {
        resourceEntries = new List<ResourceEntry>();
    }

    private float GetResourceDemand(GameResource _id) {
        float output = 0;
        for (int i = 0; i < resourceEntries.Count; i++) {
            if (resourceEntries[i].ResourceId == _id && resourceEntries[i].ChangeOnTick) {
                output += resourceEntries[i].Change;
            }
        }
        return output;
    }

    private float GetResourceSupply(GameResource _id) {
        float output = 0;
        for (int i = 0; i < resourceEntries.Count; i++) {
            if (resourceEntries[i].ResourceId == _id && !resourceEntries[i].ChangeOnTick) {
                output += resourceEntries[i].Change;
            }
        }
        return output;
    }

    #endregion
}
