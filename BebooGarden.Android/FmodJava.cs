using Android.Content;
using Android.Runtime;
using System;

namespace BebooGarden.Droid;

/// <summary>
/// FMOD's Java half, called straight through JNI.
///
/// Android's FMOD is not only libfmod.so. org.fmod.FMOD has to be handed the Context before any
/// system can be created - that is how the engine reaches the audio device and the asset manager -
/// and without it System_Create returns ERR_INTERNAL and says nothing more useful than that.
///
/// The obvious way to call it is to let the build generate a C# binding for the jar
/// (AndroidLibrary Bind="true"). That was tried and it broke the app before a line of managed code
/// ran: every launch died with
///
///   java.lang.UnsatisfiedLinkError: No implementation found for
///   MainActivity.n_onCreate(android.os.Bundle)
///
/// The .NET runtime was starting fine - its own logs were there - but the JNI methods on the
/// activity were never registered, so Android could not call into managed code at all. Adding a
/// bound library disturbs how those registrations are generated, and the failure lands before any
/// of our error handling exists, which makes it a horrible thing to diagnose: no crash log, no
/// speech, just a splash screen and a dead process.
///
/// Two calls do not justify that risk. The jar still ships, so the classes are in the dex; this
/// just reaches them by name, the way the rest of the world calls Java from C#.
/// </summary>
internal static class FmodJava
{
  private const string ClassName = "org/fmod/FMOD";

  /// <summary>Hands FMOD the Context. Must happen before the first system is created.</summary>
  internal static void Init(Context context)
  {
    IntPtr cls = JNIEnv.FindClass(ClassName);
    try
    {
      IntPtr method = JNIEnv.GetStaticMethodID(cls, "init", "(Landroid/content/Context;)V");
      if (method == IntPtr.Zero)
        throw new InvalidOperationException($"{ClassName}.init not found - is fmod.jar packaged?");

      JNIEnv.CallStaticVoidMethod(cls, method, new JValue(context));
    }
    finally
    {
      JNIEnv.DeleteGlobalRef(cls);
    }
  }

  /// <summary>Gives back what <see cref="Init"/> took.</summary>
  internal static void Close()
  {
    IntPtr cls = JNIEnv.FindClass(ClassName);
    try
    {
      IntPtr method = JNIEnv.GetStaticMethodID(cls, "close", "()V");
      if (method != IntPtr.Zero) JNIEnv.CallStaticVoidMethod(cls, method);
    }
    finally
    {
      JNIEnv.DeleteGlobalRef(cls);
    }
  }
}
