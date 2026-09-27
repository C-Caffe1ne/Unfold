-- Input only. Artwork and timing are authored in source pose timelines.
type Action = { pressed: Property<boolean>?, dragged: Property<boolean>? }
local function init(self: Action, context: Context): boolean
    local vm = context:viewModel()
    if not vm then return false end
    self.pressed = vm:getBoolean('pressed')
    self.dragged = vm:getBoolean('dragged')
    return self.pressed ~= nil and self.dragged ~= nil
end
local function performAction(self: Action, _context: ListenerContext)
    if self.dragged then self.dragged.value = false end
    if self.pressed then self.pressed.value = true end
end
return function(): ListenerAction<Action>
    return { pressed = nil, dragged = nil, init = init, performAction = performAction }
end
