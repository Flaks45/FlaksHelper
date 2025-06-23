local drawableSprite = require("structs.drawable_sprite")
local drawableLine = require("structs.drawable_line")
local drawableNinePatch = require("structs.drawable_nine_patch")
local drawableRectangle = require("structs.drawable_rectangle")
local utils = require("utils")
local atlases = require("atlases")

easeTypes = {
    "None",
    "Linear",
    "SineIn",
    "SineOut",
    "SineInOut",
    "QuadIn",
    "QuadOut",
    "QuadInOut",
    "CubeIn",
    "CubeOut",
    "CubeInOut",
    "QuintIn",
    "QuintOut",
    "QuintInOut",
    "ExpoIn",
    "ExpoOut",
    "ExpoInOut",
    "BackIn",
    "BackOut",
    "BackInOut",
    "BigBackIn",
    "BigBackOut",
    "BigBackInOut",
    "ElasticIn",
    "ElasticOut",
    "ElasticInOut",
    "BounceIn",
    "BounceOut",
    "BounceInOut"
}

local curvedZipMover = {}

local function themeTextures(entity)
    local prefix = entity.spritePath or "objects/zipmover/"
    return {
        nodeCog = prefix .. "cog",
        lights = prefix .. "light01",
        block = prefix .. "block"
    }
end

local blockNinePatchOptions = {
    mode = "border",
    borderMode = "repeat"
}

local centerColor = {0, 0, 0}
local defaultRopeColor = "663931"


curvedZipMover.name = "FlaksHelper/CurvedZipMover"
curvedZipMover.depth = -9999
curvedZipMover.nodeVisibility = "never"
curvedZipMover.nodeLineRenderType = "line"
curvedZipMover.nodeLimits = {2, -1}
curvedZipMover.minimumSize = {16, 16}
curvedZipMover.placements = {
    name = "Curved Zip Mover",
    data = {
        width = 16,
        height = 16,
        spritePath = "objects/zipmover/",
        soundEvent = "event:/game/01_forsaken_city/zip_mover",
        ropeColor = "663931",
        ropeLightColor = "9b6157",
        velocity = 60.0,
        velocityReturn = 15.0,
        returnToStart = true,
        drawBlackBorder = false,
        easing = "SineIn",
        easingReturn = "SineIn"
    }
}

curvedZipMover.fieldInformation = {
    spritePath = {
        fieldType = "string",
        allowEmpty = false
    },
    soundEvent = {
        fieldType = "string",
        allowEmpty = false
    },
    ropeColor = {
        fieldType = "color",
        allowEmpty = false
    },
    ropeLightColor = {
        fieldType = "color",
        allowEmpty = false
    },
    velocity = {
        fieldType = "number",
        allowEmpty = false,
        minimumValue = 0.0
    },
    velocityReturn = {
        fieldType = "number",
        allowEmpty = false,
        minimumValue = 0.0
    },
    returnToStart = {
        fieldType = "boolean",
        allowEmpty = false,
    },
    drawBlackBorder = {
        fieldType = "boolean",
        allowEmpty = false,
    },
    easing = {
        fieldType = "list",
        elementOptions = {
            options = easeTypes,
            editable = false
        },
        minimumElements = 1,
        maximumElements = 1,
        allowEmpty = false
    },
    easingReturn = {
        fieldType = "list",
        elementOptions = {
            options = easeTypes,
            editable = false
        },
        minimumElements = 1,
        maximumElements = 1,
        allowEmpty = false
    }
}

local function bezierPoint(points, t)
    local n = #points
    local temp = {}

    for i = 1, n do
        temp[i] = {points[i][1], points[i][2]}
    end

    -- De Casteljau's algorithm
    for r = 1, n - 1 do
        for i = 1, n - r do
            temp[i][1] = (1 - t) * temp[i][1] + t * temp[i + 1][1]
            temp[i][2] = (1 - t) * temp[i][2] + t * temp[i + 1][2]
        end
    end

    return temp[1]
end


local function perpendicularOffset(p0, p1, distance)
    local dx = p1[1] - p0[1]
    local dy = p1[2] - p0[2]
    local length = math.sqrt(dx * dx + dy * dy)

    if length == 0 then
        return {0, 0}
    end

    return {
        -dy / length * distance,
        dx / length * distance
    }
end

local function getOffsetBezierPoints(controlPoints, segments, offset)
    local pointsLeft = {}
    local pointsRight = {}

    for i = 0, segments do
        local t = i / segments
        local point = bezierPoint(controlPoints, t)

        local dirStart, dirEnd

        if i == 0 then
            dirStart = point
            dirEnd = bezierPoint(controlPoints, (i + 1) / segments)
        elseif i == segments then
            dirStart = bezierPoint(controlPoints, (i - 1) / segments)
            dirEnd = point
        else
            local prev = bezierPoint(controlPoints, (i - 1) / segments)
            local next = bezierPoint(controlPoints, (i + 1) / segments)
            dirStart = prev
            dirEnd = next
        end

        local perp = perpendicularOffset(dirStart, dirEnd, offset)

        table.insert(pointsLeft, point[1] + perp[1])
        table.insert(pointsLeft, point[2] + perp[2])

        table.insert(pointsRight, point[1] - perp[1])
        table.insert(pointsRight, point[2] - perp[2])
    end

    return pointsLeft, pointsRight
end


local function addNodeSprites(sprites, entity, cogTexture, controlPoints)
    local lastPoint = controlPoints[#controlPoints]
    local nodeCogSprite = drawableSprite.fromTexture(cogTexture, entity)
    nodeCogSprite:setPosition(lastPoint[1], lastPoint[2])
    nodeCogSprite:setJustification(0.5, 0.5)

    local segments = 24
    local offset = 4

    local leftPoints, rightPoints = getOffsetBezierPoints(controlPoints, segments, offset)

    local leftLine = drawableLine.fromPoints(leftPoints, entity.ropeColor or defaultRopeColor, 1)
    leftLine.depth = 5000
    for _, sprite in ipairs(leftLine:getDrawableSprite()) do
        table.insert(sprites, sprite)
    end

    local rightLine = drawableLine.fromPoints(rightPoints, entity.ropeColor or defaultRopeColor, 1)
    rightLine.depth = 5000
    for _, sprite in ipairs(rightLine:getDrawableSprite()) do
        table.insert(sprites, sprite)
    end

    table.insert(sprites, nodeCogSprite)
end


local function addBlockSprites(sprites, entity, blockTexture, lightsTexture, x, y, width, height)
    local rectangle = drawableRectangle.fromRectangle("fill", x + 2, y + 2, width - 4, height - 4, centerColor)

    local frameNinePatch = drawableNinePatch.fromTexture(blockTexture, blockNinePatchOptions, x, y, width, height)
    local frameSprites = frameNinePatch:getDrawableSprite()

    local lightsSprite = drawableSprite.fromTexture(lightsTexture, entity)

    lightsSprite:addPosition(math.floor(width / 2), 0)
    lightsSprite:setJustification(0.5, 0.0)

    if entity.drawBlackBorder then table.insert(sprites, drawableRectangle.fromRectangle("fill", x - 1, y - 1, width + 2, height + 2, {0, 0, 0, 1})) end

    table.insert(sprites, rectangle:getDrawableSprite())

    for _, sprite in ipairs(frameSprites) do
        table.insert(sprites, sprite)
    end

    table.insert(sprites, lightsSprite)
end

function curvedZipMover.sprite(room, entity)
    local sprites = {}

    local x, y = entity.x or 0, entity.y or 0
    local width, height = entity.width or 16, entity.height or 16
    local halfWidth, halfHeight = math.floor(width / 2), math.floor(height / 2)

    local nodes = entity.nodes or {}
    local numNodes = #nodes

    if numNodes < 1 then
        return sprites
    end

    local textures = themeTextures(entity)
    local cogTexture = textures.nodeCog

    local controlPoints = {
        {x + halfWidth, y + halfHeight}
    }

    if numNodes > 1 then
        for i = 1, numNodes - 1 do
            local node = nodes[i]
            table.insert(controlPoints, {node.x + halfWidth, node.y + halfHeight})
        end
    end

    local destNode = nodes[numNodes]
    local destX, destY = destNode.x + halfWidth, destNode.y + halfHeight
    table.insert(controlPoints, {destX, destY})

    local destCogSprite = drawableSprite.fromTexture(cogTexture, entity)
    destCogSprite:setPosition(destX, destY)
    destCogSprite:setJustification(0.5, 0.5)
    table.insert(sprites, destCogSprite)

    local segments = 24
    local offset = 4
    local ropeColor = entity.ropeColor or defaultRopeColor

    local leftPoints, rightPoints = getOffsetBezierPoints(controlPoints, segments, offset)

    local leftLine = drawableLine.fromPoints(leftPoints, ropeColor, 1)
    leftLine.depth = 5000
    for _, sprite in ipairs(leftLine:getDrawableSprite()) do
        table.insert(sprites, sprite)
    end

    local rightLine = drawableLine.fromPoints(rightPoints, ropeColor, 1)
    rightLine.depth = 5000
    for _, sprite in ipairs(rightLine:getDrawableSprite()) do
        table.insert(sprites, sprite)
    end

    addBlockSprites(sprites, entity, textures.block, textures.lights, x, y, width, height)

    return sprites
end

function curvedZipMover.selection(room, entity)
    local x, y = entity.x or 0, entity.y or 0
    local width, height = entity.width or 8, entity.height or 8
    local halfWidth, halfHeight = math.floor(width / 2), math.floor(height / 2)

    local nodes = entity.nodes or {}
    local rectangles = {}

    local cogSprite = drawableSprite.fromTexture(themeTextures(entity).nodeCog, entity)
    local cogWidth, cogHeight = cogSprite.meta.width, cogSprite.meta.height

    for _, node in ipairs(nodes) do
        local nodeX, nodeY = node.x + halfWidth, node.y + halfHeight
        local rect = utils.rectangle(nodeX - math.floor(cogWidth / 2), nodeY - math.floor(cogHeight / 2), cogWidth, cogHeight)
        table.insert(rectangles, rect)
    end

    local mainRectangle = utils.rectangle(x, y, width, height)
    return mainRectangle, rectangles
end

return curvedZipMover
