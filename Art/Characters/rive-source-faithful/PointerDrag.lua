type Action = { dragged: Property<boolean>? }
local function init(self: Action, context: Context): boolean
    local vm = context:viewModel()
    if not vm then return false end
    self.dragged = vm:getBoolean('dragged')
    return self.dragged ~= nil
end
local function performAction(self: Action, _context: ListenerContext)
    if self.dragged then self.dragged.value = true end
end
return function(): ListenerAction<Action>
    return { dragged = nil, init = init, performAction = performAction }
end
