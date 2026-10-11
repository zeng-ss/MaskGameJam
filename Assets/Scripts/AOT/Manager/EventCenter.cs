using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class IEventInfo
{
    
}

public class GameEventInfo : IEventInfo
{
    public UnityAction actions;

    public GameEventInfo(UnityAction action)
    {
        actions += action;
    }
}

public class GameEventInfo<T> : IEventInfo
{
    public UnityAction<T> actions;

    public GameEventInfo(UnityAction<T> action)
    {
        actions += action;
    }
}

public class GameEventInfo<T,K> : IEventInfo
{
    public UnityAction<T,K> actions;

    public GameEventInfo(UnityAction<T,K> action)
    {
        actions += action;
    }
}


public class EventCenter : SingleTon<EventCenter>
{
    //事件字典 
    public Dictionary<GameEvent, IEventInfo> eventDict = new Dictionary<GameEvent, IEventInfo>();

    /// <summary>
    /// 添加事件监听
    /// </summary>
    /// <param name="eventName">事件名字</param>
    /// <param name="action">要执行的方法</param>
    public void AddEventListener(GameEvent eventName, UnityAction action)
    {
        if (eventDict.ContainsKey(eventName))
        {
            (eventDict[eventName] as GameEventInfo).actions += action;
        }
        else
        {
            eventDict.Add(eventName, new GameEventInfo(action));
        }
    }

    public void AddEventListener<T>(GameEvent eventName, UnityAction<T> action)
    {
        if (eventDict.ContainsKey(eventName))
        {
            (eventDict[eventName] as GameEventInfo<T>).actions += action;
        }
        else
        {
            eventDict.Add(eventName, new GameEventInfo<T>(action));
        }
    }
    
    public void AddEventListener<T,K>(GameEvent eventName, UnityAction<T,K> action)
    {
        if (eventDict.ContainsKey(eventName))
        {
            (eventDict[eventName] as GameEventInfo<T,K>).actions += action;
        }
        else
        {
            eventDict.Add(eventName, new GameEventInfo<T,K>(action));
        }
    }

    /// <summary>
    /// 移除事件监听  
    /// </summary>
    /// <param name="eventName"></param>
    /// <param name="action"></param>
    public void RemoveEventListener(GameEvent eventName, UnityAction action)
    {
        if (eventDict.ContainsKey(eventName))
        {
            (eventDict[eventName] as GameEventInfo).actions -= action;
        }
    }

    public void RemoveEventListener<T,K>(GameEvent eventName, UnityAction<T,K> action)
    {
        if (eventDict.ContainsKey(eventName))
        {
            (eventDict[eventName] as GameEventInfo<T,K>).actions -= action;
        }
    }
    
    public void RemoveEventListener<T>(GameEvent eventName, UnityAction<T> action)
    {
        if (eventDict.ContainsKey(eventName))
        {
            (eventDict[eventName] as GameEventInfo<T>).actions -= action;
        }
    }
/// <summary>
/// 事件触发 
/// </summary>
/// <param name="eventName"></param>
    public void EventTrigger(GameEvent eventName)
    {
        if (eventDict.ContainsKey(eventName))
        {
            (eventDict[eventName] as GameEventInfo).actions?.Invoke();  
        }
        
    }
    public void EventTrigger<T>(GameEvent eventName,T info)
    {
        if (eventDict.ContainsKey(eventName))
        {
            (eventDict[eventName] as GameEventInfo<T>).actions?.Invoke(info);
        }
        
    }
    
    public void EventTrigger<T,K>(GameEvent eventName,T v1,K v2)
    {
        if (eventDict.ContainsKey(eventName))
        {
            (eventDict[eventName] as GameEventInfo<T,K>).actions?.Invoke(v1,v2);
        }
    }
    
    /// <summary>
    /// 清空字典 
    /// </summary>
    public void Clear()
    {
        eventDict.Clear();
    }
    
    
    
}