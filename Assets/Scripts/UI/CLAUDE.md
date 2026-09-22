# UI conventions
## Panels
- when building UI panels, ensure:
	- the UI controller is attached to the Panel GameObject and is enabled/active.
	- the Panel GameObject has a child GameObject "renderer" containing all UI elements.
	- the UI controller code only toggles on and off the "renderer" so the monobehaviour stays active on the parent
	
## Tabs
- when making a tabbed UI structure, follow these guidelines
	- ask about if any tabs should be hidden based on some criteria
	- color each tab based on which tab is currently active
	

## Events
Whenever one would use an event or action, use the EventService. This keeps the code easier to maintain.

### Event usage
- to add an event listener: `GameManager.EventService.Add<T>(S);`
	- T : event type, stored in Events.cs
	- S : function to call when event is invoked
- to dispatch an event: `GameManager.EventService.Dispatch(new CustomEvent(EventData));`
	- CustomEvent: The Event class created in Events.cs
	- EventData:
- create an event to be sent without any data:
	- give the class a useful name
```
public class MyCustomEvent { }
```
- create an event to be sent with data:
	- you may add multiple pieces of data to be sent with the event
```
public class MyCustomEventWithData: IEvent
{
    public float EventData

    public CurrencyRewardEvent(float eventData)
    {
        EventData = eventData;
    }
}
```
