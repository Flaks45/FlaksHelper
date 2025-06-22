local drawableSprite = require("structs.drawable_sprite")
local drawableLine = require("structs.drawable_line")
local drawableNinePatch = require("structs.drawable_nine_patch")
local drawableRectangle = require("structs.drawable_rectangle")
local utils = require("utils")
local atlases = require("atlases")

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
curvedZipMover.nodeLimits = {2, 2}
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
    }
}

curvedZipMover.fieldInformation = {
    ropeColor = {
        fieldType = "color"
    },
    ropeLightColor = {
        fieldType = "color"
    }
}

local function quadraticBezier(p0, p1, p2, t)
    local oneMinusT = 1 - t
    local x = oneMinusT * oneMinusT * p0[1] + 2 * oneMinusT * t * p1[1] + t * t * p2[1]
    local y = oneMinusT * oneMinusT * p0[2] + 2 * oneMinusT * t * p1[2] + t * t * p2[2]
    return {x, y}
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

local function getOffsetBezierPoints(p0, p1, p2, segments, offset)
    local pointsLeft = {}
    local pointsRight = {}

    for i = 0, segments do
        local t = i / segments
        local point = quadraticBezier(p0, p1, p2, t)

        local dirStart, dirEnd

        if i == 0 then
            dirStart = point
            dirEnd = quadraticBezier(p0, p1, p2, (i + 1) / segments)
        elseif i == segments then
            dirStart = quadraticBezier(p0, p1, p2, (i - 1) / segments)
            dirEnd = point
        else
            local prev = quadraticBezier(p0, p1, p2, (i - 1) / segments)
            local next = quadraticBezier(p0, p1, p2, (i + 1) / segments)
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



local function addNodeSprites(sprites, entity, cogTexture, centerX, centerY, centerNodeX, centerNodeY)
    local nodeCogSprite = drawableSprite.fromTexture(cogTexture, entity)
    nodeCogSprite:setPosition(centerNodeX, centerNodeY)
    nodeCogSprite:setJustification(0.5, 0.5)

    local controlX = (centerX + centerNodeX) / 2
    local controlY = (centerY + centerNodeY) / 2 - 12  

    local segments = 24
    local offset = 4
    local p0 = {centerX, centerY}
    local p1 = {controlX, controlY}
    local p2 = {centerNodeX, centerNodeY}

    local leftPoints, rightPoints = getOffsetBezierPoints(p0, p1, p2, segments, offset)

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

    if #nodes < 2 then
        return sprites
    end

    local centerX, centerY = x + halfWidth, y + halfHeight
    local controlX, controlY = nodes[1].x + halfWidth, nodes[1].y + halfHeight
    local destX, destY = nodes[2].x + halfWidth, nodes[2].y + halfHeight


    local textures = themeTextures(entity)
    local cogTexture = textures.nodeCog

    local destCogSprite = drawableSprite.fromTexture(cogTexture, entity)
    destCogSprite:setPosition(destX, destY)
    destCogSprite:setJustification(0.5, 0.5)
    table.insert(sprites, destCogSprite)

    local segments = 24
    local offset = 4
    local p0 = {centerX, centerY}
    local p1 = {controlX, controlY}
    local p2 = {destX, destY}

    local leftPoints, rightPoints = getOffsetBezierPoints(p0, p1, p2, segments, offset)

    local ropeColor = entity.ropeColor or defaultRopeColor

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
