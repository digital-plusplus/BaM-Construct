using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Services.Vivox;
using UnityEngine;

public class RosterManager : MonoBehaviour
{
    public GameObject rosterItemPrefab;

    Dictionary<string, List<RosterItem>> m_RosterObjects = new Dictionary<string, List<RosterItem>>();

    private void Awake()
    {
        VivoxService.Instance.ParticipantAddedToChannel += OnParticipantAdded;
        VivoxService.Instance.ParticipantRemovedFromChannel += OnParticipantRemoved;
        VivoxService.Instance.LoggedOut += OnUserLoggedOut;
        VivoxService.Instance.ChannelLeft += OnChannelDisconnected;
    }

    private void OnDestroy()
    {
        VivoxService.Instance.ParticipantAddedToChannel -= OnParticipantAdded;
        VivoxService.Instance.ParticipantRemovedFromChannel -= OnParticipantRemoved;
        VivoxService.Instance.LoggedOut -= OnUserLoggedOut;
        VivoxService.Instance.ChannelLeft -= OnChannelDisconnected;
    }

    public void ClearAllRosters()
    {
        foreach (List<RosterItem> rosterList in m_RosterObjects.Values)
        {
            foreach (RosterItem item in rosterList)
            {
                Destroy(item.gameObject);
            }
            rosterList.Clear();
        }
        m_RosterObjects.Clear();
    }

    public void ClearChannelRoster(string channelName)
    {
        List<RosterItem> rosterList = m_RosterObjects[channelName];
        foreach (RosterItem item in rosterList)
        {
            Destroy(item.gameObject);
        }
        rosterList.Clear();
        m_RosterObjects.Remove(channelName);
    }

    void CleanRoster(string channelName)
    {
        RectTransform rt = this.gameObject.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, m_RosterObjects[channelName].Count * 50);
    }

    void OnChannelDisconnected(string channelName)
    {
        if (m_RosterObjects.Keys.Contains(channelName))
        {
            ClearChannelRoster(channelName);
        }
    }

    void OnUserLoggedOut()
    {
        ClearAllRosters();
    }
    
    void OnParticipantAdded(VivoxParticipant participant)
    {
        GameObject newRosterObject = GameObject.Instantiate(rosterItemPrefab, this.gameObject.transform);
        RosterItem newRosterItem = newRosterObject.GetComponent<RosterItem>();
        List<RosterItem> thisChannelList;

        if (m_RosterObjects.ContainsKey(participant.ChannelName))
        {
            //Add this object to an existing roster
            m_RosterObjects.TryGetValue(participant.ChannelName, out thisChannelList);
            newRosterItem.SetupRosterItem(participant);
            thisChannelList.Add(newRosterItem);
            m_RosterObjects[participant.ChannelName] = thisChannelList;
        }
        else
        {
            //Create a new roster to add this object to
            thisChannelList = new List<RosterItem>();
            thisChannelList.Add(newRosterItem);
            newRosterItem.SetupRosterItem(participant);
            m_RosterObjects.Add(participant.ChannelName, thisChannelList);
        }
        CleanRoster(participant.ChannelName);
    }

    void OnParticipantRemoved(VivoxParticipant participant)
    {
        if (m_RosterObjects.ContainsKey(participant.ChannelName))
        {
            RosterItem removedItem = m_RosterObjects[participant.ChannelName].FirstOrDefault(p => p.Participant.PlayerId == participant.PlayerId);
            if (removedItem != null)
            {
                m_RosterObjects[participant.ChannelName].Remove(removedItem);
                Destroy(removedItem.gameObject);
                CleanRoster(participant.ChannelName);
            }
            else
            {
                Debug.LogError("Trying to remove a participant that has no roster item.");
            }
        }
    }
}