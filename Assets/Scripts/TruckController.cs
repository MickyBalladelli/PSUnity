using UnityEngine;
using System.Collections;

public class TruckController : MonoBehaviour {
    private NavMeshAgent agent;
    public GameObject source;
    public GameObject target;

    public void GoToTarget()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.SetDestination(target.transform.position);
    }
    public void GoToSource()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.SetDestination(source.transform.position);
    }
}
