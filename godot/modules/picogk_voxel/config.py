def can_build(env, platform):
    return platform in ["windows", "linuxbsd", "macos"]


def configure(env):
    import os

    native_dir = os.environ.get("PICOGK_NATIVE_DIR", "")
    if native_dir:
        env.Append(CPPDEFINES=[("PICOGK_NATIVE_DIR", '\\"%s\\"' % native_dir.replace("\\", "/"))])

    env.Append(CPPDEFINES=["PICOGK_VOXEL_MODULE_ENABLED=1"])
