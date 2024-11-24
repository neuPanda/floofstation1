using Content.Client.UserInterface.Controls;
using Content.Shared.CCVar;
using Content.Shared.Consent;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Consent.UI.Windows;

[GenerateTypedNameReferences]
public sealed partial class ConsentWindow : FancyWindow
{
    [Dependency] private readonly IClientConsentManager _consentManager = default!;
    [Dependency] private readonly IConfigurationManager _configManager = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;
    private readonly List<EntryState> _entries = new();

    public ConsentWindow()
    {
        RobustXamlLoader.Load(this);
        IoCManager.InjectDependencies(this);

        SaveConsentSettings.OnPressed += _ =>
        {
            SaveConsentSettings.Disabled = true;
            _consentManager.UpdateConsent(GetSettings());
        };

        _consentManager.OnServerDataLoaded += UpdateUi;
        if (_consentManager.HasLoaded)
            UpdateUi();

        ConsentFreetext.Placeholder = new Rope.Leaf(Loc.GetString("consent-window-freetext-placeholder"));
        ConsentFreetext.OnTextChanged += _ => UnsavedChanges();
    }

    private PlayerConsentSettings GetSettings()
    {
        var text = Rope.Collapse(ConsentFreetext.TextRope);
        var toggles = new Dictionary<ProtoId<ConsentTogglePrototype>, string>();

        foreach (var entry in _entries)
        {
            if (entry.Button != null && entry.Button.Pressed)
                toggles[entry.Consent.ID] = "on";
        }

        return new(text, toggles);
    }

    private void UnsavedChanges()
    {
        // Validate freetext length
        var maxLength = _configManager.GetCVar(CCVars.ConsentFreetextMaxLength);
        var length = Rope.Collapse(ConsentFreetext.TextRope).Length;

        if (length > maxLength)
        {
            SaveLabel.Text = Loc.GetString("consent-window-char-limit-warning", ("length", length), ("maxLength", maxLength));
            SaveConsentSettings.Disabled = true;

            return;
        }

        // If everything is valid, enable save button and inform user they need to save.
        SaveLabel.Text = Loc.GetString("consent-window-unsaved-changes");
        SaveConsentSettings.Disabled = false;
    }

    private void AddConsentEntry(ConsentTogglePrototype prototype)
    {
        var state = new EntryState { Consent = prototype };

        var container = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };

        var header = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            Margin = new Thickness(5f, 5f)
        };

        var name = new Label
        {
            Text = Loc.GetString($"consent-{prototype.ID}-name"),
            HorizontalExpand = true
        };

        var buttonOff = new Button { Text = "Off" };
        buttonOff.StyleClasses.Add("OpenRight");
        buttonOff.Pressed = true;

        var buttonOn = new Button { Text = "On" };
        buttonOn.StyleClasses.Add("OpenLeft");
        state.Button = buttonOn;

        buttonOff.OnPressed += _ => ButtonOnPress(buttonOff, buttonOn);
        buttonOn.OnPressed += _ => ButtonOnPress(buttonOn, buttonOff);

        var consent = _consentManager.GetConsent();
        foreach (var toggle in consent.Toggles)
        {
            if (toggle.Key == prototype.ID && toggle.Value == "on")
            {
                buttonOn.Pressed = true;
                buttonOff.Pressed = false;
                continue;
            }
        }

        header.AddChild(name);
        header.AddChild(buttonOff);
        header.AddChild(buttonOn);

        container.AddChild(header);

        var desc = new Label
        {
            Text = Loc.GetString($"consent-{prototype.ID}-desc"),
        };

        container.AddChild(desc);

        var wrapper = new PanelContainer();
        wrapper.StyleClasses.Add("PdaBorderRect");

        wrapper.AddChild(container);
        ConsentList.AddChild(wrapper);

        _entries.Add(state);
    }

    private void ButtonOnPress(Button currentButton, Button otherbutton)
    {
        currentButton.Pressed = true;
        otherbutton.Pressed = false;
        UnsavedChanges();
    }

    private void ClearOldConsentList()
    {
        ConsentList.RemoveAllChildren();
    }

    public void UpdateUi()
    {
        var consent = _consentManager.GetConsent();

        ConsentFreetext.TextRope = new Rope.Leaf(consent.Freetext);

        ClearOldConsentList();

        _entries.Clear();

        var consentprototypelist = _protoManager.EnumeratePrototypes<ConsentTogglePrototype>();
        foreach (var prototype in consentprototypelist)
            AddConsentEntry(prototype);

        SaveConsentSettings.Disabled = true;
        SaveLabel.Text = "";
    }

    private struct EntryState
    {
        public ConsentTogglePrototype Consent;
        public Button? Button;
    }
}
