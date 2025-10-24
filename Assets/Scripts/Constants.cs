public static class Constants
{
    // Websocket
    public const string SERVICE_PATH = "/Control";
    public const float MODEL_UPDATE_FPS = 60f;

    // input
    public const int ORBIT_TOUCH_COUNT = 1;
    public const int ZOOM_TOUCH_COUNT = 2;
    public const int MIN_PAN_TOUCH_COUNT = 3;
    public const int ROTATE_TOUCH_COUNT = 5;
    public const int SMOOTH_SAMPLES_COUNT = 5;
    public const float SWIPTE_THRESHOLD_PIXELS = 100f;
    public const float PINCH_REGISTER_THRESHOLD = 20f;
    public const float LONG_PRESS_THRESHOLD = 0.5f;
    public const float MAX_HOLD_MOVEMENT_PIXELS = 500f;
    public const float DOUBLE_TAP_TIME_THRESHOLD = 0.3f;

    // Control parameters
    public const float PAN_SENSITIVITY = 0.01f;
    public const float ORBIT_SENSITIVITY = 0.5f;
    public const float ZOOM_SENSITIVITY = 0.1f;
    public const float ROLL_SENSITIVITY = 0.5f;
    public const float SCALE_MIN = 0.1f;
    public const float SCALE_MAX = 10.0f;
    public const float PRESET_VIEW_ROTATION_STEP = 45f;
    public const float PRESET_VIEW_ANIMATION_DURATION = 0.4f;
    public const float AUTO_ROTATION_SPEED = 15f;
    public const float AXIS_LENGTH = 10f;
    public const float AXIS_THICKNESS = 0.03f;
    public const float ARROWHEAD_RADIUS_FACTOR = 2.5f;
    public const float ARROWHEAD_HEIGHT_FACTOR = 3f;
    public const float MODEL_THUMBNAIL_GLIDE_FRICTION = 3f;
    public const float MODEL_THUMBNAIL_MAX_VELOCITY = 1200f;
    public const float MODEL_THUMBNAIL_RESET_VELOCITY = 100f;

    // Slice feature
    public const float PLANE_SCALE_FACTOR = 50f;
    public const float SEPARATION_FACTOR = 0.1f;
    public const float SEPARATION_ANIMATION_DURATION = 0.3f;
    public const float PLANE_DEPTH = 10f;
    public const float LINE_DURATION = 0.5f;
    public const float VISUAL_DEPTH_OFFSET = 0.005f;
    public const float MIN_DRAG_DISTANCE_SQUARED = 4f;

    // server
    public const float WIGGLE_ANGLE = 7f;
    public const float WIGGLE_SPEED = 10f;

   
    //commands
    public const string UPDATE_MODEL_TRANSFORM = "UPDATE_MODEL_TRANSFORM";
    public const string UPDATE_CAMERA_TRANSFORM = "UPDATE_CAMERA_TRANSFORM";
    public const string LOAD_MODEL = "LOAD_MODEL";
    public const string UNLOAD_MODEL = "UNLOAD_MODEL";
    public const string UPDATE_VISUAL_CROP_PLANE = "UPDATE_VISUAL_CROP_PLANE";
    public const string EXECUTE_SLICE_ACTION = "EXECUTE_SLICE_ACTION";
    public const string EXECUTE_DESTROY_ACTION = "EXECUTE_DESTROY_ACTION";
    public const string START_SHAKE = "START_SHAKE";
    public const string STOP_SHAKE = "STOP_SHAKE";
    public const string UNDO_ACTION = "UNDO_ACTION";
    public const string REDO_ACTION = "REDO_ACTION";
    public const string RESET_ALL = "RESET_ALL";
    public const string UPDATE_CUT_LINE = "UPDATE_CUT_LINE";
    public const string HIDE_CUT_LINE = "HIDE_CUT_LINE";
    public const string SHOW_SLICE_ICON = "SHOW_SLICE_ICON";
    public const string HIDE_SLICE_ICON = "HIDE_SLICE_ICON";
}


