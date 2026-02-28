using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class Issue016PlayModeSmokeTests
{
    private const float DefaultFixedDeltaTime = 0.02f;

    private Scene testScene;
    private Type gameManagerType;
    private Type playerControllerType;
    private Type timeAbilityType;
    private Type uiHudType;

    private struct RuntimeGraph
    {
        public Component GameManager;
        public Component PlayerController;
        public Component TimeAbility;
    }

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        ResolveTypes();
        ClearTestOverrides();
        DestroyGameManagerInstanceIfAny();
        ResetGlobalTimeState();

        testScene = SceneManager.CreateScene($"Issue016_{Guid.NewGuid():N}");
        SceneManager.SetActiveScene(testScene);
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        ClearTestOverrides();
        ResetGlobalTimeState();
        DestroyGameManagerInstanceIfAny();

        if (testScene.IsValid() && testScene.isLoaded)
        {
            foreach (GameObject root in testScene.GetRootGameObjects())
            {
                UnityEngine.Object.Destroy(root);
            }
        }

        yield return null;
    }

    [UnityTest]
    public IEnumerator StartupRunProgression_EntersPlayingWithFullSlowMeter()
    {
        CreateMainCamera();
        RuntimeGraph graph = CreateRuntimeGraph(0.05f);

        yield return null;

        Assert.IsTrue(GetProperty<bool>(graph.GameManager, "IsPlaying"));
        float remaining = GetProperty<float>(graph.TimeAbility, "RemainingSeconds");
        float max = GetProperty<float>(graph.TimeAbility, "MaxSlowSeconds");
        Assert.That(remaining, Is.EqualTo(max).Within(0.0001f));
    }

    [UnityTest]
    public IEnumerator InputMovementBehavior_MovesWithInjectedPointerAndRespectsClamp()
    {
        Camera camera = CreateMainCamera();
        GameObject playerObject = new GameObject("Player");
        playerObject.transform.position = Vector3.zero;
        Component player = playerObject.AddComponent(playerControllerType);

        InvokeStatic(playerControllerType, "SetDeltaTimeOverrideForTests", 0.1f);

        Vector3 centerScreen = camera.WorldToScreenPoint(Vector3.zero);
        Vector2 pointerCenter = new Vector2(centerScreen.x, centerScreen.y);
        InvokeStatic(playerControllerType, "SetMovementPointerOverrideForTests", true, pointerCenter, -1);
        yield return null;

        Vector3 farRightScreen = camera.WorldToScreenPoint(new Vector3(25f, 0f, 0f));
        Vector2 pointerFarRight = new Vector2(farRightScreen.x, farRightScreen.y);
        InvokeStatic(playerControllerType, "SetMovementPointerOverrideForTests", true, pointerFarRight, -1);
        for (int i = 0; i < 8; i++)
        {
            yield return null;
        }

        float maxX = GetProperty<float>(player, "MaxX");
        float rightX = playerObject.transform.position.x;
        Assert.Greater(rightX, 0.05f);
        Assert.LessOrEqual(rightX, maxX + 0.05f);

        Vector3 farLeftScreen = camera.WorldToScreenPoint(new Vector3(-25f, 0f, 0f));
        Vector2 pointerFarLeft = new Vector2(farLeftScreen.x, farLeftScreen.y);
        InvokeStatic(playerControllerType, "SetMovementPointerOverrideForTests", true, pointerFarLeft, -1);
        for (int i = 0; i < 8; i++)
        {
            yield return null;
        }

        float leftX = playerObject.transform.position.x;
        Assert.Less(leftX, rightX - 0.05f);
        Assert.GreaterOrEqual(leftX, -maxX - 0.05f);
    }

    [UnityTest]
    public IEnumerator SlowMoDrain_DepletesMeterAndReturnsToNormalTime()
    {
        GameObject abilityObject = new GameObject("TimeAbility");
        Component ability = abilityObject.AddComponent(timeAbilityType);

        InvokeStatic(timeAbilityType, "SetSlowHoldOverrideForTests", true);
        InvokeStatic(timeAbilityType, "SetUnscaledDeltaTimeOverrideForTests", 0.25f);

        yield return null;

        Assert.IsTrue(GetProperty<bool>(ability, "SlowActive"));
        Assert.Less(Time.timeScale, 1f);

        for (int i = 0; i < 5; i++)
        {
            yield return null;
        }

        float remaining = GetProperty<float>(ability, "RemainingSeconds");
        Assert.That(remaining, Is.EqualTo(0f).Within(0.001f));
        Assert.IsFalse(GetProperty<bool>(ability, "SlowActive"));
        Assert.That(Time.timeScale, Is.EqualTo(1f).Within(0.0001f));
    }

    [UnityTest]
    public IEnumerator DeathRestartLoop_KillTransitionsAndAutoRestarts()
    {
        CreateMainCamera();
        RuntimeGraph graph = CreateRuntimeGraph(0.05f);

        yield return null;
        Assert.IsTrue(GetProperty<bool>(graph.GameManager, "IsPlaying"));

        graph.PlayerController.transform.position = new Vector3(2f, 0f, 0f);
        InvokeInstance(graph.GameManager, "KillPlayer");

        Assert.IsFalse(GetProperty<bool>(graph.GameManager, "IsPlaying"));

        yield return new WaitForSecondsRealtime(0.08f);

        Assert.IsTrue(GetProperty<bool>(graph.GameManager, "IsPlaying"));
        Assert.That(graph.PlayerController.transform.position.x, Is.EqualTo(0f).Within(0.01f));
    }

    private RuntimeGraph CreateRuntimeGraph(float restartDelaySeconds)
    {
        GameObject playerObject = new GameObject("Player");
        playerObject.transform.position = Vector3.zero;
        Component player = playerObject.AddComponent(playerControllerType);

        GameObject abilityObject = new GameObject("TimeAbility");
        Component ability = abilityObject.AddComponent(timeAbilityType);

        GameObject managerObject = new GameObject("GameManager");
        Component manager = managerObject.AddComponent(gameManagerType);

        SetPrivateField(manager, "restartDelaySeconds", restartDelaySeconds);

        MethodInfo configure = gameManagerType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(method =>
            {
                if (method.Name != "Configure")
                {
                    return false;
                }

                ParameterInfo[] parameters = method.GetParameters();
                return parameters.Length == 3 &&
                       parameters[0].ParameterType == playerControllerType &&
                       parameters[1].ParameterType == timeAbilityType &&
                       parameters[2].ParameterType == uiHudType;
            });

        Assert.NotNull(configure, "Expected GameManager.Configure(PlayerController, TimeAbility, UIHud).");
        configure.Invoke(manager, new object[] { player, ability, null });

        return new RuntimeGraph
        {
            GameManager = manager,
            PlayerController = player,
            TimeAbility = ability
        };
    }

    private static Camera CreateMainCamera()
    {
        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";

        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        camera.transform.position = new Vector3(0f, 0f, -10f);

        return camera;
    }

    private void ResolveTypes()
    {
        gameManagerType = FindType("GameManager");
        playerControllerType = FindType("PlayerController");
        timeAbilityType = FindType("TimeAbility");
        uiHudType = FindType("UIHud");

        Assert.NotNull(gameManagerType, "GameManager type not found.");
        Assert.NotNull(playerControllerType, "PlayerController type not found.");
        Assert.NotNull(timeAbilityType, "TimeAbility type not found.");
        Assert.NotNull(uiHudType, "UIHud type not found.");
    }

    private static Type FindType(string typeName)
    {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            Type type = assemblies[i].GetType(typeName);
            if (type != null)
            {
                return type;
            }
        }

        return null;
    }

    private void ClearTestOverrides()
    {
        if (playerControllerType != null)
        {
            TryInvokeStatic(playerControllerType, "ClearMovementPointerOverrideForTests");
            TryInvokeStatic(playerControllerType, "ClearDeltaTimeOverrideForTests");
        }

        if (timeAbilityType != null)
        {
            TryInvokeStatic(timeAbilityType, "ClearSlowHoldOverrideForTests");
            TryInvokeStatic(timeAbilityType, "ClearUnscaledDeltaTimeOverrideForTests");
        }
    }

    private void DestroyGameManagerInstanceIfAny()
    {
        if (gameManagerType == null)
        {
            return;
        }

        PropertyInfo instanceProperty = gameManagerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
        if (instanceProperty == null)
        {
            return;
        }

        Component instance = instanceProperty.GetValue(null) as Component;
        if (instance != null)
        {
            UnityEngine.Object.DestroyImmediate(instance.gameObject);
        }
    }

    private static void ResetGlobalTimeState()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = DefaultFixedDeltaTime;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field, $"Expected private field '{fieldName}'.");
        field.SetValue(target, value);
    }

    private static T GetProperty<T>(object target, string propertyName)
    {
        PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(property, $"Expected public property '{propertyName}'.");
        return (T)property.GetValue(target);
    }

    private static void InvokeInstance(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(method, $"Expected public method '{methodName}'.");
        method.Invoke(target, Array.Empty<object>());
    }

    private static void InvokeStatic(Type targetType, string methodName, params object[] args)
    {
        MethodInfo method = targetType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(method, $"Expected public static method '{targetType.Name}.{methodName}'.");
        method.Invoke(null, args);
    }

    private static void TryInvokeStatic(Type targetType, string methodName)
    {
        MethodInfo method = targetType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
        if (method == null)
        {
            return;
        }

        method.Invoke(null, Array.Empty<object>());
    }
}
