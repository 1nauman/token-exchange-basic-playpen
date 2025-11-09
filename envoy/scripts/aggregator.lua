-- This script performs the simple token swap.
function envoy_on_request(request_handle)
	local internal_jwt = request_handle:headers():get("x-internal-jwt")
	if internal_jwt then
		request_handle:headers():replace("Authorization", "Bearer " .. internal_jwt)
		request_handle:headers():remove("x-internal-jwt")
	end
end