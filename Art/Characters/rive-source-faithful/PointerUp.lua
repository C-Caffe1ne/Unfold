type Action = { pressed: Property<boolean>? }
local function init(self: Action, context: Context): boolean
    local vm = context:viewModel()
    if not vm then return false end
    self.pressed = vm:getBoolean('pressed')
    return self.pressed ~= nil
end
local function performAction(self: Action, _context: ListenerContext)
    if self.pressed then self.pressed.value = false end
end
return function(): ListenerAction<Action>
    return { pressed = nil, init = init, performAction = performAction }
end
