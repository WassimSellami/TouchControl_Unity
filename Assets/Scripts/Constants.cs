public static class Constants
{
    // Websocket
    public const string SERVICE_PATH = "/Control";
    public const string DEFAULT_IP_ADDRESS = "192.168.0.35";
    public const int DEFAULT_PORT = 8070;
    public const int MODEL_UPDATE_FPS = 90;
    public const float CONNECTION_TIMEOUT = 5f;

    // input
    public const int ORBIT_TOUCH_COUNT = 1;
    public const int ZOOM_TOUCH_COUNT = 2;
    public const int MIN_PAN_TOUCH_COUNT = 3;
    public const int ROTATE_TOUCH_COUNT = 5;
    public const int SMOOTH_SAMPLES_COUNT = 5;
    public const float SWIPE_THRESHOLD_PIXELS = 100f;
    public const float PINCH_REGISTER_THRESHOLD = 20f;
    public const float LONG_PRESS_THRESHOLD = 0.5f;
    public const float MAX_HOLD_MOVEMENT_PIXELS = 15f;
    public const float DOUBLE_TAP_TIME_THRESHOLD = 0.3f;

    // Control parameters
    public const float PAN_SENSITIVITY = 0.002f;
    public const float ORBIT_SENSITIVITY = 0.1f;
    public const float ZOOM_SENSITIVITY = 0.003f;
    public const float ZOOM_MAX_DISTANCE = 500f;
    public const float ROLL_SENSITIVITY = 1f;
    public const float SCALE_MIN = 0.00001f;
    public const float SCALE_MAX = 10000.0f;
    public const float PRESET_VIEW_ROTATION_STEP = 60f;
    public const float PRESET_VIEW_ANIMATION_DURATION = 0.4f;
    public const float AUTO_ROTATION_SPEED = 40f;
    public const float AXIS_LENGTH = 10f;
    public const float AXIS_THICKNESS = 0.03f;
    public const float ARROWHEAD_RADIUS_FACTOR = 2.5f;
    public const float ARROWHEAD_HEIGHT_FACTOR = 3f;
    public const float MODEL_THUMBNAIL_GLIDE_FRICTION = 3f;
    public const float MODEL_THUMBNAIL_MAX_VELOCITY = 3000f;
    public const float MODEL_THUMBNAIL_RESET_VELOCITY = 1500f;
    public const float FLICK_THRESHOLD = 9000f;

    // Slice feature
    public const float PLANE_SCALE_FACTOR = 3f;
    public const float SEPARATION_FACTOR = 0.25f;
    public const float SEPARATION_ANIMATION_DURATION = 0.7f;
    public const float PLANE_DEPTH = 10f;
    public const float LINE_DURATION = 0.5f;
    public const float VISUAL_DEPTH_OFFSET = 0.005f;
    public const float MIN_DRAG_DISTANCE_SQUARED = 4f;
    public const float ICON_VERTICAL_OFFSET_PERCENT = 0.05f;

    // server
    public const float WIGGLE_ANGLE = 2f;
    public const float WIGGLE_SPEED = 50f;
    public const float POLYGONAL_DROPDOWN_INDEX = 1;
    public const float VOLUMETRIC_DROPDOWN_INDEX = 0;


    //commands
    public const string CANCEL_LOAD = "CANCEL_LOAD";
    public const string TOGGLE_AXES = "TOGGLE_AXES";
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
    public const string MODELS_LIST_UPDATE = "MODELS_LIST_UPDATE";
    public const string MODEL_SIZE_UPDATE = "MODEL_SIZE_UPDATE";
    public const string UPDATE_VOLUME_DENSITY = "UPDATE_VOLUME_DENSITY";
}