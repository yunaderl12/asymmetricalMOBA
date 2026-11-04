using UnityEngine;
using UnityEngine.AI;

namespace Game.Core
{
    /// NavMeshAgent で歩くもの（魔物と BOT）の高さを、削れた地面に合わせる。経路は作り直さず、見た目と当たりの高さだけを下げる。
    public class GroundFollower : MonoBehaviour
    {
        private const float FollowSpeed = 3f;

        private NavMeshAgent agent;
        private float baseOffset;
        private float current;

        private void Start()
        {
            agent = GetComponent<NavMeshAgent>();
            if (agent != null) baseOffset = agent.baseOffset;
        }

        private void Update()
        {
            if (!GroundDig.HasHoles || agent == null) return;
            current = Mathf.MoveTowards(current, GroundDig.DepthAt(transform.position), FollowSpeed * Time.deltaTime);
            agent.baseOffset = baseOffset - current;
        }
    }
}
