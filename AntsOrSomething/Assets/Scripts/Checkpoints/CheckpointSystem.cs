﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[SelectionBase]
public class CheckpointSystem : MonoBehaviour
{
    public static CheckpointSystem Instance;

    public List<Checkpoint> Checkpoints { get; } = new List<Checkpoint>();

    public static int EntityCount => CheckpointTracker.Instances.Count;

    public TimeSpan Elapsed => new TimeSpan((long) (m_elapsedTime * 1e7f)); // 10'000'000 ticks in 1 second

    [FormerlySerializedAs("m_lapCount")] [Header("Core")]
    public uint LapCount = 3;

    [Header("Particles")]
    public GameObject m_start;

    public GameObject m_checkpoint;
    public GameObject m_finish;

    private float m_elapsedTime;
    private bool m_started;

    [NonSerialized] public bool FirstFinished;

    private void Awake()
    {
        Instance = this;

        UpdateIndicesAndNames();
    }

    private IEnumerator Start()
    {
        while (IntroCamera.Active)
            yield return new WaitForFixedUpdate();

        InvokeRepeating(nameof(UpdatePositions), 0f, 0.2f);

        foreach (var antAgent in AntAgent.s_instances)
        {
            StartCoroutine(antAgent.Countdown());
        }

        yield return AntPlayer.Instance.Countdown();

        m_started = true;
        FireParticles(Checkpoints[0].transform.position);
        StartCoroutine(AntPlayer.Instance.BeginRace());
        foreach (var antAgent in AntAgent.s_instances)
        {
            StartCoroutine(antAgent.BeginRace());
        }
    }

    private void Update()
    {
        if (m_started)
            m_elapsedTime += Time.deltaTime;
    }

    private void UpdatePositions()
    {
        CheckpointTracker.Instances.Sort((a, b) =>
        {
            if (a.Finished && b.Finished)
                return a.CurrentTime.CompareTo(b.CurrentTime);
            if (a.Finished)
                return -1;
            if (b.Finished)
                return 1;

            var compare = b.Lap.CompareTo(a.Lap);
            if (compare != 0)
                return compare;

            compare = b.CheckpointIndex.CompareTo(a.CheckpointIndex);
            return compare != 0 ? compare : a.Distance.CompareTo(b.Distance);
        });
    }

    private void FireParticles(Vector3 position)
    {
        if (!m_start)
            return;

        Instantiate(m_start, position, Quaternion.identity);
    }

    [ContextMenu("Update Indices, Names and Sibling Index")]
    public void UpdateIndicesAndNames()
    {
        Checkpoints.Clear();

        foreach (var checkpoint in GetComponentsInChildren<Checkpoint>())
        {
            Checkpoints.Add(checkpoint);
        }

        if (Checkpoints.Count < 2)
            throw new ArgumentOutOfRangeException($"At least 2 checkpoints are needed");

        Checkpoints.Sort((a, b) => a.Index > b.Index ? 1 : -1);

        var size = Checkpoints.Count;
        for (var i = 0; i < size; i++)
        {
            var index = i + 1 < size ? (uint) i : uint.MaxValue;

            Checkpoints[i].Index = index;
            Checkpoints[i].name = $"Checkpoint {index:D2}";
            Checkpoints[i].Next = i + 1 < size ? Checkpoints[i + 1] : Checkpoints[0];
            Checkpoints[i].transform.SetSiblingIndex(i);
        }
    }

    [ContextMenu("Insert Checkpoint")]
    public void InsertCheckpoint()
    {
        for (uint i = 0; i < transform.childCount; i++)
        {
            var index = i + 1 < transform.childCount ? i : uint.MaxValue;
            transform.GetChild((int) i).GetComponent<Checkpoint>().Index = index;
        }

        UpdateIndicesAndNames();
    }

    private void OnDrawGizmosSelected()
    {
        var size = Checkpoints.Count;
        Gizmos.color = Color.green * new Color(0.75f, 0.75f, 0.75f, 1f);

        for (var i = 0; i < size; i++)
        {
            if (i == 0)
            {
                Gizmos.DrawLine(Checkpoints[size - 1].transform.position,
                    Checkpoints[0].transform.position);
                continue;
            }

            Gizmos.DrawLine(Checkpoints[i - 1].transform.position, Checkpoints[i].transform.position);
        }
    }
}